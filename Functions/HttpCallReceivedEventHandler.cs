using System.Net;
using Azure.Communication.CallAutomation;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CallApp.Functions;

public class HttpCallReceivedEventHandler
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;

    public HttpCallReceivedEventHandler(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _logger = loggerFactory.CreateLogger<HttpCallReceivedEventHandler>();
        _configuration = configuration;
    }

    [Function("CallReceivedOptionsEventHandler")]
    public HttpResponseData CallReceivedOptions([HttpTrigger(AuthorizationLevel.Anonymous, "options", Route = "call/received")] HttpRequestData req)
    {
        _logger.LogInformation("Received OPTIONS request");
        var response = req.CreateResponse(HttpStatusCode.OK);
        string requestOriginAllowed = req.Headers.First(x => x.Key.ToLower() == "webhook-request-origin").Value.First();
    
        response.Headers.Add("Webhook-Allowed-Origin", requestOriginAllowed);
        return response;
    }

    [Function("CallReceivedEventHandler")]
    public async Task<HttpResponseData> CallReceived([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "call/received")] HttpRequestData req)
    {
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var cloudEvents = CloudEvent.ParseMany(BinaryData.FromString(requestBody), skipValidation: true).ToList();
        foreach (var cloudEvent in cloudEvents)
        {
            var callAutomationClient = new CallAutomationClient(configuration["AcsConnectionString"]);
            if (cloudEvent.Type == SystemEventNames.AcsIncomingCall)
            {
                _logger.LogInformation("Received AcsIncomingCall event");
                var callData = cloudEvent.Data.ToObjectFromJson<AcsIncomingCallEventData>();
                var phoneNumber = callData.FromCommunicationIdentifier.PhoneNumber.Value.Substring(1);
                var answerOptions = new AnswerCallOptions(callData.IncomingCallContext, new Uri($"{_configuration["CallbackBaseUrl"]}/api/callback?phoneNumber={phoneNumber}"))
                {
                    CallIntelligenceOptions = new CallIntelligenceOptions()
                    {
                        CognitiveServicesEndpoint = new Uri("https://ai-sandbox-service.cognitiveservices.azure.com/")
                    }
                };

                await callAutomationClient.AnswerCallAsync(answerOptions);
            }

            _logger.LogInformation("Event type: {type}, Event subject: {subject}", cloudEvent.Type, cloudEvent.Subject);
        }

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
