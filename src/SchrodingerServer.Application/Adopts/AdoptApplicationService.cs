using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AElf.Types;
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
using SchrodingerServer.Dtos.TraitsDto;
using SchrodingerServer.Ipfs;
using SchrodingerServer.Users;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
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


    public AdoptApplicationService(ILogger<AdoptApplicationService> logger, IOptionsMonitor<TraitsOptions> traitsOption,
        IAdoptImageService adoptImageService,
        IOptionsMonitor<ChainOptions> chainOptions, IAdoptGraphQLProvider adoptGraphQlProvider,
        IOptionsMonitor<CmsConfigOptions> cmsConfigOptions, IUserActionProvider userActionProvider,
        ISecretProvider secretProvider, IIpfsAppService ipfsAppService, AwsS3Client awsS3Client, IImageDispatcher imageDispatcher)
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
        _logger.LogInformation("GetAdoptImageInfoAsync, adoptId: {adoptId}, transactionHash: {transactionHash}", adoptId, input.TransactionHash);
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
            _logger.LogInformation("GetAdoptImageInfoAsync, {req} has not send request {hasSendRequest}", adoptId, hasSendRequest);
            await _imageDispatcher.DispatchAIGenerationRequest(adoptAddressId, AdoptInfo2GenerateImage(adoptInfo), adoptId);
            await _adoptImageService.MarkRequest(adoptId);

            var images = await provider.GetAIGeneratedImagesAsync(adoptId, adoptAddressId);
            output.AdoptImageInfo.Images = images;
            return output;
        }

        _logger.LogInformation("GetAdoptImageInfoAsync, {req} has not send request {hasSendRequest}", adoptId, hasSendRequest);
        output.AdoptImageInfo.Images = await provider.GetAIGeneratedImagesAsync(adoptId, adoptAddressId);
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

    public async Task<bool> IsOverLoadedAsync()
    {
        try
        {
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
                _logger.LogError("IsOverLoadedAsync get result Success fail, {resp}", response.ToString());
            }

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "IsOverLoadedAsync get result Success fail error, {err}", e.ToString());
            return true;
        }
    }


    public async Task<GetWaterMarkImageInfoOutput> GetWaterMarkImageInfoAsync(GetWaterMarkImageInfoInput input)
    {
        _logger.Info("GetWaterMarkImageInfoAsync, AdoptId: {req}, dataLength: {length}", input.AdoptId, 
            input.Image.Length);

        var resp = await UploadAndWatermarkAsync(input.Image, input.AdoptId);
        _logger.LogInformation("GetWatermarkImageResp AdoptId: {adoptId}", input.AdoptId);

        return resp;
    }

    private async Task<string> UploadToS3Async(string base64String, string fileName)
    {
        try
        {
            byte[] imageBytes = Convert.FromBase64String(base64String);
            var stream = new MemoryStream(imageBytes);
            return await _awsS3Client.UpLoadFileForNFTAsync(stream, fileName);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "upload to s3 error, {err}", e.ToString());
            return string.Empty;
        }
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

    private async Task<WatermarkResponse> GetWatermarkImageAsync(WatermarkInput input)
    {
        try
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
        catch (Exception e)
        {
            _logger.LogError(e, "Get Watermark Image Success fail error, {err}", e.ToString());
            return null;
        }
    }


    public async Task<ImageInfoForDirectAdoptionOutput> GetAdoptImageInfoForDirectAdoptionAsync(GetAdoptImageInfoInput input)
    {
        var adoptId = input.AdoptId;
        _logger.LogInformation("GetAdoptImageInfoAsync, adoptId: {adoptId}, transactionHash: {transactionHash}", adoptId, input.TransactionHash);
        var output = new ImageInfoForDirectAdoptionOutput();
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
        List<string> images;
        if (!hasSendRequest)
        {
            _logger.LogInformation("has not send request for adoptId:{adoptId}", adoptId);
            await _imageDispatcher.DispatchAIGenerationRequest(adoptAddressId, AdoptInfo2GenerateImage(adoptInfo), adoptId);
            await _adoptImageService.MarkRequest(adoptId);

            images = await provider.GetAIGeneratedImagesAsync(adoptId, adoptAddressId);
        }
        else
        {
            _logger.LogInformation("has send request for adoptId:{adoptId}", adoptId);
            images = await provider.GetAIGeneratedImagesAsync(adoptId, adoptAddressId);
        }
        
        output.AdoptImageInfo.Images = images;
        var imageCount = adoptInfo.ImageCount;
        if (!images.IsNullOrEmpty() && imageCount == 1)
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
        _logger.Info("AI generated images count: {}", images.Count);

        if (images.IsNullOrEmpty() || !images.Contains(image))
        {
            _logger.Info("Invalid adopt image, images:{}", JsonConvert.SerializeObject(images));
            throw new UserFriendlyException("Invalid adopt image");
        }

        var hasWaterMark = await _adoptImageService.HasWatermark(adoptId);
        string signature;
        if (hasWaterMark)
        {
            var info = await _adoptImageService.GetWatermarkImageInfoAsync(adoptId);
            _logger.Info("GetWatermarkImageInfo from grain, info: {info}", JsonConvert.SerializeObject(info));

            if (info == null || info.ImageUri == null || info.ResizedImage == null)
            {
                _logger.Info("Invalid watermark info, uri:{0}, resizeImage{1}", info.ImageUri, info.ResizedImage);
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
        _logger.Info("QueryAdoptInfoAsync, {adoptInfo}", JsonConvert.SerializeObject(adoptInfo));
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
}