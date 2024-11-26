using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AElf.ExceptionHandler;
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
using Newtonsoft.Json;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using SchrodingerServer.Common;
using SchrodingerServer.Common.HttpClient;
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
    private readonly IHttpProvider _httpProvider;

    private const string LockKeyPrefix = "SchrodingerServer:Auth:SignatureGrantHandler:";
    private const string SourcePortkey = "portkey";
    private const string SourceNightAElf = "nightElf";

    public SignatureGrantHandler(IUserInformationProvider userInformationProvider,
        ILogger<SignatureGrantHandler> logger, IAbpDistributedLock distributedLock,
        IHttpContextAccessor httpContextAccessor, IdentityUserManager userManager,
        IOptionsMonitor<TimeRangeOption> timeRangeOption,
        IOptionsMonitor<GraphQLOption> graphQlOption, IUserActionProvider userActionProvider, 
        IHttpProvider httpProvider)
    {
        _userInformationProvider = userInformationProvider;
        _logger = logger;
        _distributedLock = distributedLock;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _timeRangeOption = timeRangeOption;
        _graphQlOption = graphQlOption;
        _userActionProvider = userActionProvider;
        _httpProvider = httpProvider;
    }

    public string Name { get; } = "signature";

    public async Task<IActionResult> HandleAsync(ExtensionGrantContext context)
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
            AssertHelper.IsTrue(await _userActionProvider.CheckDomainAsync(registerHost), string.Format("Invalid host: {0}", registerHost));

            var publicKey = ByteArrayHelper.HexStringToByteArray(publicKeyVal);
            var signature = ByteArrayHelper.HexStringToByteArray(signatureVal);
            var signAddress = Address.FromPublicKey(publicKey);
            
            var newSignText = """
                              Welcome to Schrodinger! Click to sign in to the world's first AI-powered 404 NFT platform! This request will not trigger any blockchain transaction or cost any gas fees.

                              signature: 
                              """+string.Join("-", address, timestampVal);
            
            AssertHelper.IsTrue(CryptoHelper.RecoverPublicKey(signature,
                HashHelper.ComputeFrom(Encoding.UTF8.GetBytes(newSignText).ToHex()).ToByteArray(),
                out var managerPublicKey), "Invalid signature.");

            AssertHelper.IsTrue(CryptoHelper.RecoverPublicKey(signature,
                HashHelper.ComputeFrom(string.Join("-", address, timestampVal)).ToByteArray(),
                out var managerPublicKeyOld), "Invalid signature.");
        
            AssertHelper.IsTrue(managerPublicKey.ToHex() == publicKeyVal || managerPublicKeyOld.ToHex() == publicKeyVal, "Invalid publicKey or signature.");

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
            _logger.LogInformation("GetCaHolderInfo, signAddress: {address}, address: {address}, source: {source}", signAddress.ToBase58(), address, source);
            if (source == SourcePortkey)
            {
                var manager = signAddress.ToBase58();
                var portkeyUrl = _graphQlOption.CurrentValue.PortkeyUrl;
                var caHolderInfos = await GetCaHolderInfo(portkeyUrl, manager);
                _logger.LogInformation("GetCaHolderInfo finished, address: {address}, infos: {infos}", manager, JsonConvert.SerializeObject(caHolderInfos));
                AssertHelper.NotNull(caHolderInfos, "CaHolder not found.");
                AssertHelper.NotEmpty(caHolderInfos.CaHolderManagerInfo, "CaHolder manager not found.");
                if (caHolderInfos.CaHolderManagerInfo.Select(m => m.CaAddress).All(add => add != address))
                {
                    var caHolderManagerInfo = await GetCaHolderManagerInfoAsync(manager);
                    _logger.LogInformation("GetCaHolderManagerInfoAsync, address: {address}, infos: {infos}", manager, JsonConvert.SerializeObject(caHolderManagerInfo));
                    AssertHelper.IsTrue(caHolderManagerInfo != null && caHolderManagerInfo.CaAddress == address,
                        "PublicKey not manager of address");
                }

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
            // claimsPrincipal.SetScopes("SchrodingerServer");
            // claimsPrincipal.SetResources(await GetResourcesAsync(context, principal.GetScopes()));
            // claimsPrincipal.SetAudiences("SchrodingerServer");
            principal.SetScopes("SchrodingerServer");
            principal.SetResources(await GetResourcesAsync(context, principal.GetScopes()));
            principal.SetAudiences("SchrodingerServer");
            
            // await context.HttpContext.RequestServices.GetRequiredService<AbpOpenIddictClaimDestinationsManager>()
            //     .SetAsync(principal);
            var abpOpenIddictClaimDestinationsManager = context.HttpContext.RequestServices
                .GetRequiredService<AbpOpenIddictClaimsPrincipalManager>();
            await abpOpenIddictClaimDestinationsManager.HandleAsync(context.Request, principal);
            return new SignInResult(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, principal);
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
        // return graphQlResponse.Data;
        var indexerCaHolderInfos = graphQlResponse.Data;
        
        if (!indexerCaHolderInfos.CaHolderManagerInfo.IsNullOrEmpty())
        {
            return indexerCaHolderInfos;
        }

        var caHolderManagerInfo = await GetCaHolderManagerInfoAsync(managerAddress);
        if (caHolderManagerInfo != null)
        {
            indexerCaHolderInfos.CaHolderManagerInfo.Add(caHolderManagerInfo);
        }

        return indexerCaHolderInfos;
    }
    
    [ExceptionHandler(typeof(Exception), Message = "GetCaHolderManagerInfoAsync Failed", ReturnDefault = ReturnDefault.Default)]
    private async Task<CAHolderManager?> GetCaHolderManagerInfoAsync(string manager)
    {
        var portkeyCaHolderInfoUrl = _graphQlOption.CurrentValue.PortkeyCaHolderInfoUrl;
    
        var apiInfo = new ApiInfo(HttpMethod.Get, "/api/app/account/manager/check");
        var param = new Dictionary<string, string> { { "manager", manager } };
        var resp = await _httpProvider.InvokeAsync<CAHolderManager>(portkeyCaHolderInfoUrl, apiInfo, param: param);
        return resp;
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