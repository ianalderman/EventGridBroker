#r "Newtonsoft.Json"
#r "Microsoft.Azure.WebJobs.Extensions.EventGrid"

using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;

using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;


public async static Task Run(EventGridEvent eventGridEvent, string inputBlob, TraceWriter log)
{
    //*** Should this function instead be replaced by individual subs?  If so how do we make it easy to consume as the plan here is simple JSON to control flows..
    log.Info(eventGridEvent.ToString());

   
    //In testing a single eventGridEvent had multiple events so below we turn it into a list
    //var eventGridEvents = (List<EventGridEvent>)Newtonsoft.Json.JsonConvert.DeserializeObject(eventGridEvent.ToString(), typeof(List<EventGridEvent>));
    var routeEntries = (List<route>)Newtonsoft.Json.JsonConvert.DeserializeObject(inputBlob, typeof(List<route>));
    
    var eventData = (subscriptionEventData)Newtonsoft.Json.JsonConvert.DeserializeObject(eventGridEvent.Data.ToString(), typeof(subscriptionEventData));

    foreach (route routeEntry in routeEntries) {
        log.Info("Processing route:" + routeEntry.name);

        if (routeEntry.subscriptionId != "*" && routeEntry.subscriptionId != eventData.subscriptionId) {
             log.Info("Skipping route as event subscriptionId (" + eventData.subscriptionId + ") does not match route subscription Id (" + routeEntry.subscriptionId + ")");
             continue;
        }

        if (routeEntry.resourceProvider != "*" && routeEntry.resourceProvider != eventData.resourceProvider) {
            log.Info("Skipping route as event resourceProvider (" + eventData.resourceProvider + ") does not match route resourceProvider(" + routeEntry.resourceProvider +")");
            continue;
        }

        if (routeEntry.resourceUri != "*" && routeEntry.resourceUri != eventData.resourceUri) {
            log.Info("Skipping route as event resourceUri (" + eventData.resourceUri + ") does not match route resourceUri(" + routeEntry.resourceUri +")");
            continue;
        }
    
        log.Info("I need to do stuff with this one!");

        foreach (action act in routeEntry.actions) {
            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();

        string topicKey = "";
        try
            {
                var keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
                var URI = "https://" + System.Environment.GetEnvironmentVariable("EventGridKeyVault", EnvironmentVariableTarget.Process) + "/secrets/" + act.name;

                var secret = await keyVaultClient.GetSecretAsync(URI).ConfigureAwait(false);
                topicKey = secret.Value;
            }
        catch (Exception exp)
            {
                log.Error($"Unable to retrieve secret: {exp.Message}");
            }

        string claims = eventData.claims;

        claims = claims.Replace("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn", "upn");
        claims = claims.Replace("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname", "surname");
        claims = claims.Replace("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname", "givenname");

        string[] Uri = eventData.resourceUri.Split('/');
        string resourceGrp = Uri[4];

        dynamic objClaims = JsonConvert.DeserializeObject<dynamic>(claims as string);


        var events = new List<customEvent>();

        var eventToLog = new topicEvent();
        eventToLog.upn = objClaims.upn;
        eventToLog.user = objClaims.givenname + " " + objClaims.surname;
        eventToLog.clientIP = (string)objClaims.ipaddr;
        eventToLog.resourceGroup = Uri[4];
        eventToLog.authorization = eventData.authorization;
        eventToLog.correlationId = eventData.correlationId;
        eventToLog.httpRequest = eventData.httpRequest;
        eventToLog.resourceProvider = eventData.resourceProvider;
        eventToLog.resourceUri = eventData.resourceUri;
        eventToLog.operationName = eventData.operationName;
        eventToLog.status = eventData.status;
        eventToLog.subscriptionId = eventData.subscriptionId;
        eventToLog.tenantId = eventData.tenantId;

        events.Add(new customEvent {subject = act.name, eventTime = eventGridEvent.EventTime, eventType = act.name, data = eventToLog} );
        //events.Add(new customEvent {subject = act.name, eventTime = DateTime.UtcNow, eventType = act.name, data = eventToLog} );
        //CustomEvent.Subject = act.name;
        //CustomEvent.EventTime = eventGridEvent.EventTime.ToString();
        //CustomEvent.EventType = act.name;
        //CustomEvent.Data = eventToLog;
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("aeg-sas-key", topicKey);
        var json = JsonConvert.SerializeObject(events);
        log.Info(json);
        var msg = new StringContent(json, Encoding.UTF8, "application/json");
        log.Info(act.endPoint);
        var result = await client.PostAsync(act.endPoint, msg);
        log.Info(msg.ToString());
        log.Info("Result:" + result.ReasonPhrase);
        }
    }
}

public class customEvent {
    public string id {get; }
    public string eventType {get;set;}
    public string subject {get; set;}
    public DateTime eventTime {get; set;}
    public topicEvent data {get; set;}

    public customEvent() {
        id = Guid.NewGuid().ToString();
    }
}

public class topicEvent {

    public topicEvent() {

    }
    public string upn {get; set;}
    public string user {get; set;}
    public string clientIP {get; set;}
    public string resourceGroup {get; set;}
    public string authorization {get; set;}
    //public string claims { get; set; }
    public string correlationId { get; set; }
    public string httpRequest { get; set; }
    public string resourceProvider { get; set; }
    public string resourceUri { get; set; }
    public string operationName { get; set; }
    public string status { get; set; }
    public string subscriptionId { get; set; }
    public string tenantId { get; set; }
}

public class subscriptionEventData {
    [JsonProperty(PropertyName = "authorization")]
    public string authorization { get; set; }

    [JsonProperty(PropertyName = "claims")]
    public string claims { get; set; }

    [JsonProperty(PropertyName = "correlationId")]
    public string correlationId { get; set; }

    [JsonProperty(PropertyName = "httpRequest")]
    public string httpRequest { get; set; }

    [JsonProperty(PropertyName = "resourceProvider")]
    public string resourceProvider { get; set; }

    [JsonProperty(PropertyName = "resourceUri")]
    public string resourceUri { get; set; }

    [JsonProperty(PropertyName = "operationName")]
    public string operationName { get; set; }

    [JsonProperty(PropertyName = "status")]
    public string status { get; set; }

    [JsonProperty(PropertyName = "subscriptionId")]
    public string subscriptionId { get; set; }

    [JsonProperty(PropertyName = "tenantId")]
    public string tenantId { get; set; }
}

public class route {
    [JsonProperty(PropertyName = "name")]
    public string name { get; set; }

    [JsonProperty(PropertyName = "subscriptionId")]
    public string subscriptionId { get; set; }

    [JsonProperty(PropertyName = "resourceProvider")]
    public string resourceProvider { get; set; }

    [JsonProperty(PropertyName = "operationName")]
    public string operationName { get; set; }

    [JsonProperty(PropertyName = "resourceGroup")]
    public string resourceGroup { get; set; }

    [JsonProperty(PropertyName = "resourceUri")]
    public string resourceUri { get; set; }

    [JsonProperty(PropertyName = "actions")]
    public List<action> actions {get;set;}

    //public route() {
    //    actions = new List<action>();
    //}
}

public class action {

    [JsonProperty(PropertyName = "name")]
    public string name { get; set; }

    [JsonProperty(PropertyName = "type")]
    public string type { get; set; }

    [JsonProperty(PropertyName = "endpoint")]
    public string endPoint { get; set; }

    //public action() {}
}


