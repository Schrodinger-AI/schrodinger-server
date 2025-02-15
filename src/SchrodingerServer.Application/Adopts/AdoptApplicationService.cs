using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AElf.ExceptionHandler;
using AElf.Types;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans.Runtime;
using SchrodingerServer.Adopts.dispatcher;
using SchrodingerServer.Adopts.provider;
using SchrodingerServer.AwsS3;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dtos.Adopts;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.Dtos.TraitsDto;
using SchrodingerServer.ExceptionHandling;
using SchrodingerServer.Ipfs;
using SchrodingerServer.Options;
using SchrodingerServer.Users;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Caching;
using Volo.Abp.Users;
using ConfirmInput = Schrodinger.ConfirmInput;
using Trait = SchrodingerServer.Dtos.TraitsDto.Trait;

namespace SchrodingerServer.Adopts;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class AdoptApplicationService : ApplicationService, IAdoptApplicationService
{
    private readonly ILogger<AdoptApplicationService> _logger;
    private readonly IOptionsMonitor<TraitsOptions> _traitsOptions;
    private readonly IOptionsMonitor<CmsConfigOptions> _cmsConfigOptions;
    private readonly IAdoptImageService _adoptImageService;
    private readonly ChainOptions _chainOptions;
    private readonly IAdoptGraphQLProvider _adoptGraphQlProvider;
    private readonly IUserActionProvider _userActionProvider;
    private readonly ISecretProvider _secretProvider;
    private readonly IIpfsAppService _ipfsAppService;
    private readonly AwsS3Client _awsS3Client;
    private readonly IImageDispatcher _imageDispatcher;
    private readonly IDistributedCache<string> _distributedCache;
    private readonly IVotesProvider _votesProvider;


    public AdoptApplicationService(ILogger<AdoptApplicationService> logger, IOptionsMonitor<TraitsOptions> traitsOption,
        IAdoptImageService adoptImageService,
        IOptionsMonitor<ChainOptions> chainOptions, IAdoptGraphQLProvider adoptGraphQlProvider,
        IOptionsMonitor<CmsConfigOptions> cmsConfigOptions, IUserActionProvider userActionProvider,
        ISecretProvider secretProvider, IIpfsAppService ipfsAppService, AwsS3Client awsS3Client, 
        IImageDispatcher imageDispatcher, 
        IDistributedCache<string> distributedCache, IVotesProvider votesProvider)
    {
        _logger = logger;
        _traitsOptions = traitsOption;
        _adoptImageService = adoptImageService;
        _adoptGraphQlProvider = adoptGraphQlProvider;
        _chainOptions = chainOptions.CurrentValue;
        _cmsConfigOptions = cmsConfigOptions;
        _userActionProvider = userActionProvider;
        _secretProvider = secretProvider;
        _ipfsAppService = ipfsAppService;
        _awsS3Client = awsS3Client;
        _imageDispatcher = imageDispatcher;
        _distributedCache = distributedCache;
        _votesProvider = votesProvider;
    }

    private string GetCurChain()
    {
        var chainId = CommonConstant.MainChainId;
        const string curChainKey = "curChain";
        if (_cmsConfigOptions.CurrentValue.ConfigMap.TryGetValue(curChainKey, out var curChain))
        {
            chainId = curChain;
        }

        return chainId;
    }

    public async Task<GetAdoptImageInfoOutput> GetAdoptImageInfoAsync(GetAdoptImageInfoInput input)
    {
        var adoptId = input.AdoptId;
        _logger.LogInformation("GetAdoptImageInfoAsync Begin, adoptId: {adoptId}, transactionHash: {transactionHash}", adoptId, input.TransactionHash);
        var output = new GetAdoptImageInfoOutput();
        var adoptInfo = await QueryAdoptInfoAsync(adoptId);
        if (adoptInfo == null)
        {
            return output;
        }

        output.AdoptImageInfo = new AdoptImageInfo
        {
            Attributes = adoptInfo.Attributes,
            Generation = adoptInfo.Generation,
        };
        var aelfAddress = await _userActionProvider.GetCurrentUserAddressAsync(GetCurChain());
        var adoptAddressId = ImageProviderHelper.JoinAdoptIdAndAelfAddress(adoptId, aelfAddress);
        var provider = _imageDispatcher.CurrentProvider();
        var hasSendRequest = await _adoptImageService.HasSendRequest(adoptId) && await provider.HasRequestId(adoptAddressId);
        if (!hasSendRequest)
        {
            _logger.LogInformation("send ai generation request for: {id}", adoptId);
            await _imageDispatcher.DispatchAIGenerationRequest(adoptAddressId, AdoptInfo2GenerateImage(adoptInfo), adoptId);
            await _adoptImageService.MarkRequest(adoptId);

            var images = await provider.GetAIGeneratedImagesAsync(adoptId, adoptAddressId);
            output.AdoptImageInfo.Images = images;
            return output;
        }
        
        output.AdoptImageInfo.Images = await provider.GetAIGeneratedImagesAsync(adoptId, adoptAddressId);
        if (output.AdoptImageInfo.Images.IsNullOrEmpty())
        {
            _logger.LogInformation("waiting for ai generation for: {id}", adoptId);
        }
        else
        {
            _logger.LogInformation("query success for: {id}", adoptId);
        }
        
        return output;
    }

    private GenerateImage AdoptInfo2GenerateImage(AdoptInfo adoptInfo)
    {
        var seed = CurrentUser.IsAuthenticated
            ? BitConverter.ToInt32(CurrentUser.GetId().ToByteArray(), 0)
            : new Random().Next();
        var imageInfo = new GenerateImage
        {
            seed = seed,
            newAttributes = new List<Trait> { },
            baseImage = new BaseImage
            {
                attributes = new List<Trait> { }
            },
            numImages = adoptInfo.ImageCount
        };
        foreach (var item in adoptInfo.Attributes.Select(attributeItem => new Trait
                 {
                     traitType = attributeItem.TraitType,
                     value = attributeItem.Value
                 }))
        {
            imageInfo.newAttributes.Add(item);
        }

        return imageInfo;
    }

    [ExceptionHandler(typeof(Exception), Message = "Check IsOverLoadedAsync Failed", TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleException))]
    public async Task<bool> IsOverLoadedAsync()
    {
        if (_traitsOptions.CurrentValue.UnderMaintenance)
        {
            return false;
        }
        
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(_traitsOptions.CurrentValue.IsOverLoadedUrl);
        if (response.IsSuccessStatusCode)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("IsOverLoadedAsync get result Success");
            var resp = JsonConvert.DeserializeObject<IsOverLoadedResponse>(responseString);
            return resp.isOverLoaded;
        }
        else
        {
            _logger.LogError("IsOverLoadedAsync get result fail, {resp}", response.ToString());
        }

        return true;    
    }


    public async Task<GetWaterMarkImageInfoOutput> GetWaterMarkImageInfoAsync(GetWaterMarkImageInfoInput input)
    {
        _logger.LogInformation("GetWaterMarkImageInfoAsync, AdoptId: {req}, dataLength: {length}", input.AdoptId, 
            input.Image.Length);

        var resp = await UploadAndWatermarkAsync(input.Image, input.AdoptId);
        _logger.LogInformation("GetWatermarkImageResp AdoptId: {adoptId}", input.AdoptId);

        return resp;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionStr))]
    private async Task<string> UploadToS3Async(string base64String, string fileName)
    {
        byte[] imageBytes = Convert.FromBase64String(base64String);
        var stream = new MemoryStream(imageBytes);
        return await _awsS3Client.UpLoadFileForNFTAsync(stream, fileName);
    }

    private string GenerateSignature(byte[] privateKey, string adoptId, string image)
    {
        var data = new ConfirmInput
        {
            AdoptId = Hash.LoadFromHex(adoptId),
            Image = image
        };
        var dataHash = HashHelper.ComputeFrom(data);
        var signature = CryptoHelper.SignWithPrivateKey(privateKey, dataHash.ToByteArray());
        return signature.ToHex();
    }

    private async Task<string> GenerateSignatureWithSecretService(string adoptId, string uri, string image)
    {
        var data = new ConfirmInput
        {
            AdoptId = Hash.LoadFromHex(adoptId),
            Image = image,
            ImageUri = uri
        };
        var dataHash = HashHelper.ComputeFrom(data);
        var start = DateTime.Now;
        var signature = await _secretProvider.GetSignatureFromHashAsync(_chainOptions.PublicKey, dataHash);
        var cost = (DateTime.Now - start).TotalMilliseconds;
        _logger.LogInformation("GetSignatureFromHash timeCost={cost}", cost);
        return signature;
    }

    private async Task<List<string>> GetImagesAsync(string adoptId, string requestId)
    {
        var images = await _adoptImageService.GetImagesAsync(adoptId);
        _logger.LogInformation("TraitsActionProvider GetImagesAsync images {requestId} {adoptId} count={count}", requestId, adoptId, images?.Count ?? 0);
        return images;
        // return await _defaultImageProvider.GenerateImageAsync(requestId, adoptId);
    }

    private async Task<AdoptInfo> QueryAdoptInfoAsync(string adoptId)
    {
        return await _adoptGraphQlProvider.QueryAdoptInfoAsync(adoptId);
    }

    [ExceptionHandler(typeof(Exception), Message = "Get WatermarkImageAsync Failed", ReturnDefault = ReturnDefault.Default, TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    private async Task<WatermarkResponse> GetWatermarkImageAsync(WatermarkInput input)
    {
 
        using var httpClient = new HttpClient();
        var jsonString = ImageProviderHelper.ConvertObjectToJsonString(input);
        var requestContent = new StringContent(jsonString, Encoding.UTF8, "application/json");
        httpClient.DefaultRequestHeaders.Add("accept", "*/*");
        var start = DateTime.Now;
        var response = await httpClient.PostAsync(_traitsOptions.CurrentValue.ImageProcessUrl, requestContent);
        var cost = (DateTime.Now - start).TotalMilliseconds;
        if (response.IsSuccessStatusCode)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Get Watermark Image Success timeCost={cost}", cost);

            var resp = JsonConvert.DeserializeObject<WatermarkResponse>(responseString);

            return resp;
        }
        else
        {
            _logger.LogError("Get Watermark Image Success fail, {resp}", response.ToString());
        }

        return null;
    }


    public async Task<ImageInfoForDirectAdoptionOutput> GetAdoptImageInfoForDirectAdoptionAsync(GetAdoptImageInfoInput input)
    {
        var adoptId = input.AdoptId;
        _logger.LogInformation("GetAdoptImageInfoForDirectAdoptionAsync, adoptId: {adoptId}, transactionHash: {transactionHash}", adoptId, input.TransactionHash);
        var output = new ImageInfoForDirectAdoptionOutput();
        var adoptInfo = await QueryAdoptInfoAsync(adoptId);
        if (adoptInfo == null)
        {
            return output;
        }

        var address = input.Address;
        if (address.IsNullOrEmpty())
        {
            address = await _userActionProvider.GetCurrentUserAddressAsync();
            if (address.IsNullOrEmpty())
            {
                _logger.LogInformation("GetCurrentUserAddress failed for adoption:{adoptId}}", adoptId);
            }
            
            _logger.LogInformation("GetCurrentUserAddress address for adoption:{adoptId}, addres:{address}", adoptId, address);
        }

        do
        {
            DateTime activityDeadline = new DateTime(2024, 11, 6, 0, 0, 0, DateTimeKind.Utc);
            DateTime activityBeginTime = new DateTime(2024, 10, 23, 0, 0, 0, DateTimeKind.Utc);
            
            if (input.Faction.IsNullOrEmpty() || DateTime.UtcNow > activityDeadline)
            {
                _logger.LogInformation("vote activity expired {adoptId}", adoptId);
                break;
            }

            if (adoptInfo.AdoptTime < activityBeginTime || adoptInfo.AdoptTime > activityDeadline)
            {
                _logger.LogInformation("adoption not in expire time {adoptId}", adoptId);
                break;
            }
            
            var key = "vote_" + adoptId;
            var cache = await _distributedCache.GetAsync(key);
            if (cache != null)
            {
                _logger.LogInformation("vote already found in cache, {adoptId}", adoptId);
                break;
            }
            
            var voteRecord = await _votesProvider.GetVoteAsync(adoptId);
            if (voteRecord != null && !voteRecord.AdoptId.IsNullOrEmpty())
            {
                _logger.LogInformation("vote already found in db, {adoptId}", adoptId);
                break;
            }

            if (input.Faction != "Trump" && input.Faction != "Harris")
            {
                _logger.LogInformation("vote for invalid faction: {faction}, {adoptId}}", input.Faction, adoptId);
                break;
            }
            
            _logger.LogInformation("vote for faction: {faction}, adoptId: {adoptId}, address: {address}", input.Faction, adoptId, address);
            await _distributedCache.SetAsync(key, input.Faction.ToString(),  new DistributedCacheEntryOptions()
            {
                SlidingExpiration = TimeSpan.FromHours(1)
            });

            var side = input.Faction == "Trump" ? 1 : 2;
            await _votesProvider.VoteAsync(adoptId, side, address);
        } while (false);
            
        output.AdoptImageInfo = new AdoptImageInfo
        {
            Attributes = adoptInfo.Attributes,
            Generation = adoptInfo.Generation,
            Symbol =  adoptInfo.Symbol,
            TokenName = adoptInfo.Symbol + "GEN" + adoptInfo.Generation.ToString(),
            BoxImage = BoxHelper.GetBoxImage(adoptInfo.Generation == 9, adoptInfo.Rarity)
        };
        
        if (_traitsOptions.CurrentValue.UnderMaintenance)
        {
            _logger.LogInformation("under maintenance, adoptId:{adoptId}", adoptId);
            
            if (!input.AdoptOnly)
            {
                output.UnderMaintenance = true;
            }
            
            return output;
        }
        
        var adoptAddressId = ImageProviderHelper.JoinAdoptIdAndAelfAddress(adoptId, address);
        var provider = _imageDispatcher.CurrentProvider();
        var judgement1 = await _adoptImageService.HasSendRequest(adoptId);
        var judgement2 = await provider.HasRequestId(adoptAddressId);
        _logger.LogInformation("send judgment for adoptAddressId:{adoptAddressId}, {judgement1}, {judgement2} {force}", 
            adoptAddressId,  judgement1, judgement2, input.ForceTriggerGenerate);
        
        var hasSendRequest = judgement1 && judgement2;
        
        if (!hasSendRequest || input.ForceTriggerGenerate)
        {
            _logger.LogInformation("send ai generation request for adoptId:{adoptId}, addressId:{addressId}", adoptId, adoptAddressId);
            await _imageDispatcher.DispatchAIGenerationRequest(adoptAddressId, AdoptInfo2GenerateImage(adoptInfo), adoptId);
            await _adoptImageService.MarkRequest(adoptId);
        }
        
        if (input.AdoptOnly)
        {
            return output;
        }

        var images = await provider.GetAIGeneratedImagesAsync(adoptId, adoptAddressId);
        output.AdoptImageInfo.Images = images;
        var imageCount = adoptInfo.ImageCount;
        if (!images.IsNullOrEmpty() && images.Count == 1)
        {
            _logger.LogInformation("ai generated image ready for direct adoption: {adoptId}", adoptId);
            var waterMarkedInfo =  await UploadAndWatermarkAsync(images[0], input.AdoptId);
            output.Image = waterMarkedInfo.Image;
            output.ImageUri = waterMarkedInfo.ImageUri;
            output.Signature = waterMarkedInfo.Signature;
        }
        return output;
    }

    private async Task<GetWaterMarkImageInfoOutput> UploadAndWatermarkAsync(string image, string adoptId)
    {
        var images = await _adoptImageService.GetImagesAsync(adoptId);
        _logger.LogDebug("AI generated images count: {}", images.Count);

        if (images.IsNullOrEmpty() || !images.Contains(image))
        {
            _logger.LogDebug("Invalid adopt image, images:{}", JsonConvert.SerializeObject(images));
            throw new UserFriendlyException("Invalid adopt image");
        }

        var hasWaterMark = await _adoptImageService.HasWatermark(adoptId);
        string signature;
        if (hasWaterMark)
        {
            var info = await _adoptImageService.GetWatermarkImageInfoAsync(adoptId);
            _logger.LogDebug("GetWatermarkImageInfo from grain, info: {info}", JsonConvert.SerializeObject(info));

            if (info == null || info.ImageUri == null || info.ResizedImage == null)
            {
                _logger.LogDebug("Invalid watermark info, uri:{0}, resizeImage{1}", info.ImageUri, info.ResizedImage);
                throw new UserFriendlyException("Invalid watermark info");
            }

            signature = await GenerateSignatureWithSecretService(adoptId, info.ImageUri, info.ResizedImage);
            
            if (signature.IsNullOrEmpty())
            {
                _logger.LogError("GenerateSignatureFailed adoptId: {adoptId}", adoptId);
                throw new UserFriendlyException("GenerateSignatureFailed");
            }
            
            var response = new GetWaterMarkImageInfoOutput
            {
                Image = info.ResizedImage,
                Signature = signature,
                ImageUri = info.ImageUri
            };
            return response;
        }

        var adoptInfo = await QueryAdoptInfoAsync(adoptId);
        _logger.LogDebug("QueryAdoptInfoAsync, {adoptInfo}", JsonConvert.SerializeObject(adoptInfo));
        if (adoptInfo == null)
        {
            throw new UserFriendlyException("query adopt info failed adoptId = " + adoptId);
        }

        var waterMarkInfo = await GetWatermarkImageAsync(new WatermarkInput()
        {
            sourceImage = image,
            watermark = new WaterMark
            {
                text = adoptInfo.Symbol
            }
        });

        if (waterMarkInfo == null || waterMarkInfo.processedImage == "" || waterMarkInfo.resized == "")
        {
            _logger.LogError("waterMarkImage empty, adoptId: {adoptId}", adoptId);
            throw new UserFriendlyException("waterMarkImage empty");
        }

        var stringArray = waterMarkInfo.processedImage.Split(",");
        if (stringArray.Length < 2)
        {
            _logger.LogInformation("invalid waterMarkInfo");
            throw new UserFriendlyException("invalid waterMarkInfo");
        }

        var base64String = stringArray[1].Trim();
        string waterImageHash = await _ipfsAppService.UploadFile(base64String, adoptId);
        if (waterImageHash == "")
        {
            _logger.LogInformation("upload ipfs failed");
            throw new UserFriendlyException("upload failed");
        }

        var uri = "ipfs://" + waterImageHash;

        // uploadToS3
        var s3Url = await UploadToS3Async(base64String, waterImageHash);
        _logger.LogInformation("upload to s3, url:{url}", s3Url);

        var needRemove = images.Count == 2;
        await _adoptImageService.SetWatermarkImageInfoAsync(adoptId, uri, waterMarkInfo.resized, image, needRemove);

        signature = await GenerateSignatureWithSecretService(adoptId, uri, waterMarkInfo.resized);

        if (signature.IsNullOrEmpty())
        {
            _logger.LogError("GenerateSignatureFailed adoptId: {adoptId}", adoptId);
            throw new UserFriendlyException("GenerateSignatureFailed");
        }
        
        var resp = new GetWaterMarkImageInfoOutput
        {
            Image = waterMarkInfo.resized,
            Signature = signature,
            ImageUri = uri
        };
        _logger.LogInformation("UploadAndWatermarkAsyncFinished, adoptId:{adoptId}", adoptId);

        return resp;
    }

    public async Task<ConfirmAdoptionOutput> ConfirmAdoptionAsync(ConfirmAdoptionInput input)
    {
        var adoptId = input.AdoptId;
        _logger.LogInformation("ConfirmAdoptionAsync Begin, adoptId: {adoptId}", adoptId);
        
        var adoptInfo = await QueryAdoptInfoAsync(adoptId);
        _logger.LogDebug("QueryAdoptInfoAsync, {adoptInfo}", JsonConvert.SerializeObject(adoptInfo));
        if (adoptInfo == null)
        {
            _logger.LogError("Invalid adopt id: {id}", adoptId);
            throw new UserFriendlyException("Invalid adopt id");
        }
        
        var images = await _adoptImageService.GetImagesAsync(adoptId);
        _logger.LogDebug("AI generated images count: {}", images.Count);

        if (images.IsNullOrEmpty())
        {
            _logger.LogError("adopt image not found");
            throw new UserFriendlyException("adopt image not found");
        }
        
        if (images.Count != 1)
        {
            _logger.LogError("more than 1 image");
            throw new UserFriendlyException("more than 1 image");
        }

        var hasWaterMark = await _adoptImageService.HasWatermark(adoptId);
        string signature;
        if (hasWaterMark)
        {
            var info = await _adoptImageService.GetWatermarkImageInfoAsync(adoptId);
            _logger.LogDebug("GetWatermarkImageInfo from grain, info: {info}", JsonConvert.SerializeObject(info));

            if (info == null || info.ImageUri == null || info.ResizedImage == null)
            {
                _logger.LogDebug("Invalid watermark info, uri:{0}, resizeImage{1}", info.ImageUri, info.ResizedImage);
                throw new UserFriendlyException("Invalid watermark info");
            }

            signature = await GenerateSignatureWithSecretService(adoptId, info.ImageUri, info.ResizedImage);
            
            if (signature.IsNullOrEmpty())
            {
                _logger.LogError("GenerateSignatureFailed adoptId: {adoptId}", adoptId);
                throw new UserFriendlyException("GenerateSignatureFailed");
            }
            
            var response = new ConfirmAdoptionOutput
            {
                Image = info.ResizedImage,
                Signature = signature,
                ImageUri = info.ImageUri
            };
            return response;
        }

        var image = images[0];
        var waterMarkInfo = await GetWatermarkImageAsync(new WatermarkInput()
        {
            sourceImage = image,
            watermark = new WaterMark
            {
                text = adoptInfo.Symbol
            }
        });

        if (waterMarkInfo == null || waterMarkInfo.processedImage == "" || waterMarkInfo.resized == "")
        {
            _logger.LogError("waterMarkImage empty, adoptId: {adoptId}", adoptId);
            throw new UserFriendlyException("waterMarkImage empty");
        }

        var stringArray = waterMarkInfo.processedImage.Split(",");
        if (stringArray.Length < 2)
        {
            _logger.LogInformation("invalid waterMarkInfo");
            throw new UserFriendlyException("invalid waterMarkInfo");
        }

        var base64String = stringArray[1].Trim();
        string waterImageHash = await _ipfsAppService.UploadFile(base64String, adoptId);
        if (waterImageHash == "")
        {
            _logger.LogInformation("upload ipfs failed");
            throw new UserFriendlyException("upload failed");
        }

        var uri = "ipfs://" + waterImageHash;

        // uploadToS3
        var s3Url = await UploadToS3Async(base64String, waterImageHash);
        _logger.LogInformation("upload to s3, url:{url}", s3Url);

        var needRemove = images.Count == 2;
        await _adoptImageService.SetWatermarkImageInfoAsync(adoptId, uri, waterMarkInfo.resized, image, needRemove);

        signature = await GenerateSignatureWithSecretService(adoptId, uri, waterMarkInfo.resized);

        if (signature.IsNullOrEmpty())
        {
            _logger.LogError("GenerateSignatureFailed adoptId: {adoptId}", adoptId);
            throw new UserFriendlyException("GenerateSignatureFailed");
        }
        
        var resp = new ConfirmAdoptionOutput
        {
            Image = waterMarkInfo.resized,
            Signature = signature,
            ImageUri = uri
        };
        _logger.LogInformation("ConfirmAdoptionAsync Finish, adoptId:{adoptId}", adoptId);

        return resp;
    }

    public async Task<GetVotesOutput> GetVoteAsync()
    {
        long voteCount1 = await _votesProvider.VoteCountAsync(1);
        long voteCount2 = await _votesProvider.VoteCountAsync(2);

        DateTime nowUtc = DateTime.UtcNow;
        DateTime activityDeadline = new DateTime(2024, 11, 6, 0, 0, 0, DateTimeKind.Utc);
        
        TimeSpan timeDifference = activityDeadline - nowUtc;

        return new GetVotesOutput
        {
            Countdown = (int)timeDifference.TotalSeconds,
            Votes = new List<long> { voteCount2, voteCount1 }
        };
    }
}