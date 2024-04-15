using System.Net;
using System.Text.RegularExpressions;
using Azure.Communication;
using Azure.Communication.CallAutomation;
using Azure.Messaging;
using CallApp.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CallApp.Functions;

public class HttpCallbackHandler
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly CallAutomationClient _callAutomationClient;

    public HttpCallbackHandler(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _logger = loggerFactory.CreateLogger<HttpCallbackHandler>();
        _configuration = configuration;
        _callAutomationClient = new CallAutomationClient(configuration["AcsConnectionString"]);
    }

    [Function("HttpCallbackHandler")]
    public async Task<HttpResponseData> Callback(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "callback")] HttpRequestData request)
    {
        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var cloudEvents = CloudEvent.ParseMany(BinaryData.FromString(requestBody)).ToList();
        foreach (var cloudEvent in cloudEvents)
        {
            var parsedEvent = CallAutomationEventParser.Parse(cloudEvent);
            _logger.LogInformation($"Received event of type {cloudEvent.Type} with call connection id {parsedEvent.CallConnectionId}");

            var callConnection = _callAutomationClient.GetCallConnection(parsedEvent.CallConnectionId);
            var callMedia = callConnection.GetCallMedia();

            if (parsedEvent is CallConnected callConnected)
            {
                var playSource = new TextSource("Thank you for calling The Hotline. What question can I answer?") { VoiceName = "en-US-NancyNeural" };
                var participantsResponse = await callConnection.GetParticipantsAsync();
                var targetPhoneNumber = $"+{request.Query["phoneNumber"] ?? string.Empty}";
                var recognizeOptions = new CallMediaRecognizeSpeechOptions(new PhoneNumberIdentifier(targetPhoneNumber))
                {
                    EndSilenceTimeout = TimeSpan.FromSeconds(2),
                    Prompt = playSource
                };

                await callMedia.StartRecognizingAsync(recognizeOptions);
            }

            if (parsedEvent is RecognizeCompleted recognizeSpeechCompleted)
            {
                var speechResult = (SpeechResult)recognizeSpeechCompleted.RecognizeResult;
                var match = Regex.Match(speechResult.Speech, @"\d{5}");
                var responseString = match.Success == false ? "You did not provide a valid US zipcode" : await ReturnForecastResponse(int.Parse(match.Value));
                var targetPhoneNumber = $"+{request.Query["phoneNumber"] ?? string.Empty}";

                var playSource = new TextSource(responseString) { VoiceName = "en-US-NancyNeural" };
                var playOptions = new PlayOptions(playSource, [new PhoneNumberIdentifier(targetPhoneNumber)]);

                await callMedia.PlayAsync(playOptions);
            }

            if (parsedEvent is PlayCompleted playCompleted)
            {
                _logger.LogInformation("Play completed event received");
                var participantsResponse = await callConnection.GetParticipantsAsync();
                var targetPhoneNumber = participantsResponse.Value.First(x => x.Identifier is PhoneNumberIdentifier).Identifier;

                await callConnection.RemoveParticipantAsync(targetPhoneNumber);
            }

            if (parsedEvent is CallDisconnected callDisconnected)
            {
                _logger.LogInformation($"Call disconnected event received = {callDisconnected.CallConnectionId}");          
            }
        }

        return request.CreateResponse(HttpStatusCode.OK);
    }

    async Task<string> ReturnForecastResponse(int zipcode)
    {
        var httpClient = new HttpClient()
        {
            BaseAddress = new Uri("http://dataservice.accuweather.com"),
        };

        var response = await httpClient.GetAsync($"locations/v1/cities/search?apikey={_configuration["AccuweatherApiKey"]}&q={zipcode}");
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var locationKey = JArray.Parse(responseString).First()["Key"].ToString();
        var cityName = JArray.Parse(responseString).First()["LocalizedName"].ToString();
        var stateName = JArray.Parse(responseString).First()["AdministrativeArea"]["LocalizedName"].ToString();

        response = await httpClient.GetAsync($"forecasts/v1/daily/5day/{locationKey}?apikey={_configuration["AccuweatherApiKey"]}&details=true");
        response.EnsureSuccessStatusCode();
        responseString = await response.Content.ReadAsStringAsync();
        
        var forecast = JsonConvert.DeserializeObject<ForecastModel>(JObject.Parse(responseString)["DailyForecasts"]?.ElementAtOrDefault(1)?.ToString()) ?? throw new Exception("Failed to deserialize forecast");
        
        return $"The forecast for {cityName}, {stateName} on {forecast.Date:dddd, MMMM dd} is as follows: {forecast}";
    }
}
