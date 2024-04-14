using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AElf.Types;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using SchrodingerServer.Common;
using SchrodingerServer.Dto;
using SchrodingerServer.Options;
using SchrodingerServer.Users;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Identity;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.ExtensionGrantTypes;

namespace SchrodingerServer;

public class SignatureGrantHandler : ITokenExtensionGrant, ITransientDependency
{
    private readonly IUserInformationProvider _userInformationProvider;
    private readonly ILogger<SignatureGrantHandler> _logger;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IdentityUserManager _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserActionProvider _userActionProvider;

    private readonly IOptionsMonitor<TimeRangeOption> _timeRangeOption;
    private readonly IOptionsMonitor<GraphQLOption> _graphQlOption;

    private const string LockKeyPrefix = "SchrodingerServer:Auth:SignatureGrantHandler:";
    private const string SourcePortkey = "portkey";
    private const string SourceNightAElf = "nightElf";

    public SignatureGrantHandler(IUserInformationProvider userInformationProvider,
        ILogger<SignatureGrantHandler> logger, IAbpDistributedLock distributedLock,
        IHttpContextAccessor httpContextAccessor, IdentityUserManager userManager,
        IOptionsMonitor<TimeRangeOption> timeRangeOption,
        IOptionsMonitor<GraphQLOption> graphQlOption, IUserActionProvider userActionProvider)
    {
        _userInformationProvider = userInformationProvider;
        _logger = logger;
        _distributedLock = distributedLock;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _timeRangeOption = timeRangeOption;
        _graphQlOption = graphQlOption;
        _userActionProvider = userActionProvider;
    }

    public string Name { get; } = "signature";

    public async Task<IActionResult> HandleAsync(ExtensionGrantContext context)
    {
        try
        {
            var publicKeyVal = context.Request.GetParameter("publickey").ToString();
            var signatureVal = context.Request.GetParameter("signature").ToString();
            var timestampVal = context.Request.GetParameter("timestamp").ToString();
            var address = context.Request.GetParameter("address").ToString();
            var source = context.Request.GetParameter("source").ToString();
            var registerHost = DeviceInfoContext.CurrentDeviceInfo.Host ?? CommonConstant.EmptyString;

            AssertHelper.NotEmpty(source, "invalid parameter source.");
            AssertHelper.NotEmpty(publicKeyVal, "invalid parameter publickey.");
            AssertHelper.NotEmpty(signatureVal, "invalid parameter signature.");
            AssertHelper.NotEmpty(timestampVal, "invalid parameter timestamp.");
            AssertHelper.NotEmpty(address, "invalid parameter address.");
            AssertHelper.IsTrue(long.TryParse(timestampVal, out var timestamp) && timestamp > 0,
                "invalid parameter timestamp value.");
            AssertHelper.IsTrue(await _userActionProvider.CheckDomainAsync(registerHost), string.Format("Invalid host:{0}", registerHost));

            var publicKey = ByteArrayHelper.HexStringToByteArray(publicKeyVal);
            var signature = ByteArrayHelper.HexStringToByteArray(signatureVal);
            var signAddress = Address.FromPublicKey(publicKey);

            AssertHelper.IsTrue(CryptoHelper.RecoverPublicKey(signature,
                HashHelper.ComputeFrom(string.Join("-", address, timestampVal)).ToByteArray(),
                out var managerPublicKey), "Invalid signature.");
            AssertHelper.IsTrue(managerPublicKey.ToHex() == publicKeyVal, "Invalid publicKey or signature.");

            var time = DateTime.UnixEpoch.AddMilliseconds(timestamp);
            AssertHelper.IsTrue(
                time > DateTime.UtcNow.AddMinutes(-_timeRangeOption.CurrentValue.TimeRange) &&
                time < DateTime.UtcNow.AddMinutes(_timeRangeOption.CurrentValue.TimeRange),
                "The time should be {} minutes before and after the current time.",
                _timeRangeOption.CurrentValue.TimeRange);

            var userName = string.Empty;
            var caHash = string.Empty;
            var caAddressMain = string.Empty;
            var caAddressSide = new Dictionary<string, string>();
            if (source == SourcePortkey)
            {
                var portkeyUrl = _graphQlOption.CurrentValue.PortkeyUrl;
                var caHolderInfos = await GetCaHolderInfo(portkeyUrl, signAddress.ToBase58());
                AssertHelper.NotNull(caHolderInfos, "CaHolder not found.");
                AssertHelper.NotEmpty(caHolderInfos.CaHolderManagerInfo, "CaHolder manager not found.");
                AssertHelper.IsTrue(
                    caHolderInfos.CaHolderManagerInfo.Select(m => m.CaAddress).Any(add => add == address),
                    "PublicKey not manager of address");

                //Find caHash by caAddress
                foreach (var account in caHolderInfos.CaHolderManagerInfo)
                {
                    caHash = caHolderInfos.CaHolderManagerInfo[0].CaHash;
                    if (account.ChainId == CommonConstant.MainChainId)
                    {
                        caAddressMain = account.CaAddress;
                    }
                    else
                    {
                        caAddressSide.TryAdd(account.ChainId, account.CaAddress);
                    }
                }

                userName = caHash;
            }
            else if (source == SourceNightAElf)
            {
                AssertHelper.IsTrue(address == signAddress.ToBase58(), "Invalid address or pubkey");
                userName = address;
            }
            else
            {
                throw new UserFriendlyException("Source not support.");
            }

            var user = await _userManager.FindByNameAsync(userName!);
            if (user == null)
            {
                var userId = Guid.NewGuid();
                var createUserResult = await CreateUserAsync(_userManager, _userInformationProvider, userId,
                    address!, caHash, caAddressMain, caAddressSide, registerHost);
                AssertHelper.IsTrue(createUserResult, "Create user failed.");
                user = await _userManager.GetByIdAsync(userId);
            }
            else
            {
                var userSourceInput = new UserGrainDto
                {
                    Id = user.Id,
                    AelfAddress = address!,
                    CaHash = caHash,
                    CaAddressMain = caAddressMain,
                    CaAddressSide = caAddressSide,
                    RegisterDomain = registerHost
                };
                await _userInformationProvider.SaveUserSourceAsync(userSourceInput);
            }

            var userClaimsPrincipalFactory = context.HttpContext.RequestServices
                .GetRequiredService<Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<IdentityUser>>();
            var signInManager = context.HttpContext.RequestServices
                .GetRequiredService<Microsoft.AspNetCore.Identity.SignInManager<IdentityUser>>();
            var principal = await signInManager.CreateUserPrincipalAsync(user);
            var claimsPrincipal = await userClaimsPrincipalFactory.CreateAsync(user);
            claimsPrincipal.SetScopes("SchrodingerServer");
            claimsPrincipal.SetResources(await GetResourcesAsync(context, principal.GetScopes()));
            claimsPrincipal.SetAudiences("SchrodingerServer");
            await context.HttpContext.RequestServices.GetRequiredService<AbpOpenIddictClaimDestinationsManager>()
                .SetAsync(principal);
            return new SignInResult(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, claimsPrincipal);
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning("Create token failed: {Message}", e.Message);
            return ForbidResult(OpenIddictConstants.Errors.InvalidRequest, e.Message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Create token error");
            return ForbidResult(OpenIddictConstants.Errors.ServerError, "Internal error.");
        }
    }

    private static ForbidResult ForbidResult(string errorType, string errorDescription)
    {
        return new ForbidResult(
            new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme },
            properties: new AuthenticationProperties(new Dictionary<string, string>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = errorType,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = errorDescription
            }!));
    }

    private async Task<IndexerCAHolderInfos> GetCaHolderInfo(string url, string managerAddress, string? chainId = null)
    {
        using var graphQlClient = new GraphQLHttpClient(url, new NewtonsoftJsonSerializer());

        // It should just one item
        var graphQlRequest = new GraphQLRequest
        {
            Query = @"query(
                    $manager:String
                    $skipCount:Int!,
                    $maxResultCount:Int!
                ) {
                    caHolderManagerInfo(dto: {
                        manager:$manager,
                        skipCount:$skipCount,
                        maxResultCount:$maxResultCount
                    }){
                        chainId,
                        caHash,
                        caAddress,
                        managerInfos{ address }
                    }
                }",
            Variables = new
            {
                chainId = chainId, manager = managerAddress, skipCount = 0, maxResultCount = 10
            }
        };

        var graphQlResponse = await graphQlClient.SendQueryAsync<IndexerCAHolderInfos>(graphQlRequest);
        return graphQlResponse.Data;
    }

    private async Task<bool> CreateUserAsync(IdentityUserManager userManager,
        IUserInformationProvider userInformationProvider, Guid userId, string address,
        string caHash, string caAddressMain, Dictionary<string, string> caAddressSide, string? registerDomain)
    {
        var result = false;
        await using var handle =
            await _distributedLock.TryAcquireAsync(name: LockKeyPrefix + caHash + address);

        //get shared lock
        if (handle == null)
        {
            _logger.LogError("do not get lock, keys already exits. userId: {UserId}", userId.ToString());
            return result;
        }

        var userName = string.IsNullOrEmpty(caHash) ? address : caHash;
        var user = new IdentityUser(userId, userName: userName,
            email: Guid.NewGuid().ToString("N") + "@schrodingernft.ai");
        var identityResult = await userManager.CreateAsync(user);

        if (!identityResult.Succeeded)
        {
            return identityResult.Succeeded;
        }

        _logger.LogDebug("Save user extend info...");
        var userSourceInput = new UserGrainDto()
        {
            Id = userId,
            AelfAddress = address,
            CaHash = caHash,
            CaAddressMain = caAddressMain,
            CaAddressSide = caAddressSide,
            RegisterDomain = registerDomain,
        };
        var userGrainDto = await userInformationProvider.SaveUserSourceAsync(userSourceInput);
        _logger.LogDebug("create user success: {UserId}", userId.ToString());

        return identityResult.Succeeded;
    }

    private async Task<IEnumerable<string>> GetResourcesAsync(ExtensionGrantContext context,
        ImmutableArray<string> scopes)
    {
        var resources = new List<string>();
        if (!scopes.Any())
        {
            return resources;
        }

        await foreach (var resource in context.HttpContext.RequestServices.GetRequiredService<IOpenIddictScopeManager>()
                           .ListResourcesAsync(scopes))
        {
            resources.Add(resource);
        }

        return resources;
    }
}