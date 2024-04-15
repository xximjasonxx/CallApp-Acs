using Azure.Communication;
using Azure.Communication.CallAutomation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CallApp.Functions;

public class HttpMakeCallHandler
{
    private readonly ILogger _logger;

    public HttpMakeCallHandler(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<HttpMakeCallHandler>();
    }

    [Function("HttpMakeCall")]
    public async Task<HttpResponseData> MakeCall([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "make/call")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        var client = new CallAutomationClient("endpoint=https://acs-sandbox-jx01.unitedstates.communication.azure.com/;accesskey=eFHAeHBeURxFI5uFY73GZPLse6QG5z3p1smcxspyrAp5aDWYoyt4PQugmemGmZkFwuJM8jCiMGSjVNZz81MkRw==");
        var targetPhoneIdentity = new PhoneNumberIdentifier($"+17349042053");
        var callerIdNumber = new PhoneNumberIdentifier("+18442950575");
        var callInvite = new CallInvite(targetPhoneIdentity, callerIdNumber);
        var createCallOptions = new CreateCallOptions(callInvite, new Uri("https://3138-67-38-27-213.ngrok-free.app/api/callback"))
        {
            CallIntelligenceOptions = new CallIntelligenceOptions()
            {
                CognitiveServicesEndpoint = new Uri("https://ai-sandbox-service.cognitiveservices.azure.com/")
            }
        };

        await client.CreateCallAsync(createCallOptions);
        
        return req.CreateResponse(System.Net.HttpStatusCode.OK);
    }
}
