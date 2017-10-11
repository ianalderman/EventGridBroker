#r "Newtonsoft.Json"
using System.Net;
using Newtonsoft.Json;
using System.Text;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");

    dynamic data = await req.Content.ReadAsAsync<object>();

    string dataResourceUri = data.resourceUri;
    string claims = data.claims;

    claims = claims.Replace("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn", "upn");
    claims = claims.Replace("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname", "surname");
    claims = claims.Replace("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname", "givenname");

    string[] Uri = dataResourceUri.Split('/');
    string resourceGrp = Uri[4];
    dynamic objClaims = JsonConvert.DeserializeObject<dynamic>(claims as string);

    string upn = objClaims.upn;
    string strName = objClaims.givenname + " " + objClaims.surname;
    
    string msg = "{\"clientIPAddress\":\"" + (string)objClaims.ipaddr + "\",\"resourceGroup\":\"" + resourceGrp + "\",\"upn\":\"" + upn + "\",\"name\":\"" + strName + "\"}";

    dynamic output = new {clientIPAddress = (string)objClaims.ipaddr, resourceGroup = resourceGrp, upn = objClaims.upn, name = strName};
    var jsonToReturn = JsonConvert.SerializeObject(output);

    return new HttpResponseMessage(HttpStatusCode.OK) {
        Content = new StringContent(jsonToReturn, Encoding.UTF8, "application/json")
    };
}
