using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Xml.Linq;
using SharedCoreLib.Models.VO;
using SharedCoreLib.Services.ShazamAPI;
using SharedCoreLib.Services.ShazamAPI.Responses;
using SharedCoreLib.Utils.XML;


namespace Shockzam
{
    public class ShazamClient
    {
        public static string kDoRecognitionURL = @"http://msft.shazamid.com/orbit/DoRecognition1";
        public static string kRequestResultsURL = @"http://msft.shazamid.com/orbit/RequestResults1";

        private readonly IceKey encryptKey = new IceKey(1);
        private ShazamRequest shazamRequest;

        public string deviceID { private get; set; }
        public event ShazamStateChangedCallback OnRecongnitionStateChanged;

        public int DoRecognition(byte[] audioBuffer, MicrophoneRecordingOutputFormatType formatType)
        {
            shazamRequest = new ShazamRequest();
            var shazamAPIConfig = new ShazamAPIConfig();
            shazamAPIConfig.initKey("20FB1BCBE2C4848F");
            Console.WriteLine(shazamAPIConfig.key);
            shazamRequest.token = "B540AD35";
            shazamRequest.key = shazamAPIConfig.key;
            shazamRequest.audioBuffer = audioBuffer;
            shazamRequest.deviceid = "00000000-0000-0000-0000-000000000000"; // It works
            shazamRequest.service = "cn=US,cn=V12,cn=SmartClub,cn=ShazamiD,cn=services";
            shazamRequest.language = "en-US";
            shazamRequest.model = "Microsoft Windows";
            shazamRequest.appid = "ShazamId_SmartPhone_Tau__1.3.0";

            if (deviceID != null && deviceID != "") shazamRequest.deviceid = deviceID;

            switch (formatType)
            {
                case MicrophoneRecordingOutputFormatType.PCM:
                {
                    shazamRequest.filename = "sample.wav";
                    break;
                }
                case MicrophoneRecordingOutputFormatType.MP3:
                {
                    shazamRequest.filename = "sample.mp3";
                    break;
                }
                case MicrophoneRecordingOutputFormatType.SIG:
                {
                    shazamRequest.filename = "sample.sig";
                    break;
                }
            }

            var request = shazamRequest;

            try
            {
                RaiseOnRecongnitionStateChanged(ShazamRecognitionState.Sending, null);
                var audio = request.audioBuffer;
                var audioLength = audio.Length.ToString();
                var tagDate = DateTime.Now.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss");
                var orbitPostRequestBuilder = new OrbitPostRequestBuilder(encryptKey, request.key);
                orbitPostRequestBuilder.AddEncryptedParameter("cryptToken", request.token);
                orbitPostRequestBuilder.AddEncryptedParameter("deviceId", request.deviceid);
                orbitPostRequestBuilder.AddParameter("service", request.service);
                orbitPostRequestBuilder.AddParameter("language", request.language);
                orbitPostRequestBuilder.AddEncryptedParameter("deviceModel", request.model);
                orbitPostRequestBuilder.AddEncryptedParameter("applicationIdentifier", request.appid);
                orbitPostRequestBuilder.AddEncryptedParameter("tagDate", tagDate);
                orbitPostRequestBuilder.AddEncryptedParameter("sampleBytes", audioLength);
                orbitPostRequestBuilder.AddEncryptedFile("sample", request.filename, audio, audio.Length);
                var webRequest = WebRequest.Create(kDoRecognitionURL);
                orbitPostRequestBuilder.PopulateWebRequestHeaders(webRequest);
                doTimeoutRequest(new RequestContext
                {
                    WebRequest = webRequest,
                    RequestBuilder = orbitPostRequestBuilder
                }, RecognitionReadCallback, 30000);
            }
            catch (Exception e)
            {
                RecognitionFailed(e);
            }

            return 0;
        }

        private void DoGetResult(ulong requestId)
        {
            try
            {
                RaiseOnRecongnitionStateChanged(ShazamRecognitionState.Matching, null);
                shazamRequest.requestId = requestId;
                shazamRequest.art_width = 520;
                var request = shazamRequest;
                var str = request.requestId.ToString();
                var orbitPostRequestBuilder = new OrbitPostRequestBuilder(encryptKey, request.key);
                orbitPostRequestBuilder.AddEncryptedParameter("cryptToken", request.token);
                orbitPostRequestBuilder.AddEncryptedParameter("deviceId", request.deviceid);
                orbitPostRequestBuilder.AddParameter("service", request.service);
                orbitPostRequestBuilder.AddParameter("language", request.language);
                orbitPostRequestBuilder.AddEncryptedParameter("deviceModel", request.model);
                orbitPostRequestBuilder.AddEncryptedParameter("applicationIdentifier", request.appid);
                orbitPostRequestBuilder.AddEncryptedParameter("coverartSize", request.art_width.ToString());
                orbitPostRequestBuilder.AddEncryptedParameter("requestId", str);
                var webRequest = WebRequest.Create(kRequestResultsURL);
                orbitPostRequestBuilder.PopulateWebRequestHeaders(webRequest);
                var requestContext = new RequestContext();
                requestContext.WebRequest = webRequest;
                requestContext.RequestBuilder = orbitPostRequestBuilder;
                doTimeoutRequest(requestContext, ResultReadCallback, 30000);
            }
            catch (Exception e)
            {
                RecognitionFailed(e);
            }
        }

        private void RecognitionReadCallback(IAsyncResult asynchronousResult)
        {
            RecognitionShazamResponse recognitionShazamResponse = null;
            try
            {
                var asyncState = (RequestContext) asynchronousResult.AsyncState;
                var webRequest = (HttpWebRequest) asyncState.WebRequest;
                var httpWebResponse = (HttpWebResponse) webRequest.EndGetResponse(asynchronousResult);
                var requestBuilder = asyncState.RequestBuilder as OrbitPostRequestBuilder;

                var responseString = "";
                if (httpWebResponse.GetResponseStream() != null)
                    using (var streamReader = new StreamReader(httpWebResponse.GetResponseStream()))
                    {
                        responseString = streamReader.ReadToEnd();
                    }

                recognitionShazamResponse = ParseResponseForDoRecognition(responseString);
                if (recognitionShazamResponse.errorMessage != "" && recognitionShazamResponse.errorMessage != null)
                    throw new Exception(recognitionShazamResponse.errorMessage);
                DoGetResult(recognitionShazamResponse.requestId);
            }
            catch (Exception e)
            {
                RecognitionFailed(e);
            }
        }

        private void ResultReadCallback(IAsyncResult asynchronousResult)
        {
            ResultShazamResponse resultShazamResponse = null;
            try
            {
                var asyncState = (RequestContext) asynchronousResult.AsyncState;
                var webRequest = (HttpWebRequest) asyncState.WebRequest;
                var httpWebResponse = (HttpWebResponse) webRequest.EndGetResponse(asynchronousResult);

                var responseString = "";
                if (httpWebResponse.GetResponseStream() != null)
                    using (var streamReader = new StreamReader(httpWebResponse.GetResponseStream()))
                    {
                        responseString = streamReader.ReadToEnd();
                    }

                resultShazamResponse = ParseResponseForRequestResults(responseString);
                if (resultShazamResponse.errorMessage != "" && resultShazamResponse.errorMessage != null)
                    throw new Exception(resultShazamResponse.errorMessage);
                RaiseOnRecongnitionStateChanged(ShazamRecognitionState.Done, new ShazamResponse(resultShazamResponse));
            }
            catch (Exception e)
            {
                RecognitionFailed(e);
            }
        }

        private void RaiseOnRecongnitionStateChanged(ShazamRecognitionState State, ShazamResponse Response)
        {
            if (OnRecongnitionStateChanged != null)
                RaiseEventOnUIThread(OnRecongnitionStateChanged, new object[] {State, Response});
        }

        private void RecognitionFailed(Exception e)
        {
            if (OnRecongnitionStateChanged != null)
                RaiseEventOnUIThread(OnRecongnitionStateChanged,
                    new object[] {ShazamRecognitionState.Failed, new ShazamResponse(e)});
        }

        private void RaiseEventOnUIThread(Delegate theEvent, object[] args)
        {
            try
            {
                foreach (var d in theEvent.GetInvocationList())
                {
                    var syncer = d.Target as ISynchronizeInvoke;
                    if (syncer == null)
                    {
                        d.DynamicInvoke(args);
                    }
                    else
                    {
                        Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-us");
                        syncer.BeginInvoke(d, args); // cleanup omitted
                    }
                }
            }
            catch
            {
                OnRecongnitionStateChanged((ShazamRecognitionState) args[0], (ShazamResponse) args[1]);
            }
        }

        private ResultShazamResponse ParseResponseForRequestResults(string responseString)
        {
            var resultShazamResponse = new ResultShazamResponse();
            var xDocument = XDocument.Parse(responseString);
            XNamespace xNamespace = "http://orbit.shazam.com/v1/response";
            var elementIgnoreNamespace = xDocument.Root.GetElementIgnoreNamespace(xNamespace, "requestResults1");
            var xElement = elementIgnoreNamespace;
            if (elementIgnoreNamespace == null)
            {
                var elementIgnoreNamespace1 = xDocument.Root.GetElementIgnoreNamespace(xNamespace, "error");
                var xElement1 = elementIgnoreNamespace1;
                if (elementIgnoreNamespace1 != null)
                    resultShazamResponse.errorCode = int.Parse(xElement1.Attribute("code").Value);
            }
            else
            {
                var elementIgnoreNamespace2 = xElement.GetElementIgnoreNamespace(xNamespace, "request");
                var tagVO = new TagVO();
                tagVO.Id = elementIgnoreNamespace2.Attribute("requestId").Value;
                resultShazamResponse.newTag = tagVO;
                TrackVO trackVO = null;
                var num = 0;
                var xElement2 = xElement.GetElementIgnoreNamespace(xNamespace, "tracks");
                var elementIgnoreNamespace3 = xElement2.GetElementIgnoreNamespace(xNamespace, "track");
                if (elementIgnoreNamespace3 != null)
                {
                    trackVO = ParseXmlElementForTrackData(xNamespace, elementIgnoreNamespace3, false);
                    if (elementIgnoreNamespace3.Attribute("cache-max-age") != null)
                        num = Convert.ToInt32(elementIgnoreNamespace3.Attribute("cache-max-age").Value);
                }

                if (trackVO != null) resultShazamResponse.newTag.Track = trackVO;
            }

            return resultShazamResponse;
        }

        private RecognitionShazamResponse ParseResponseForDoRecognition(string responseString)
        {
            var recognitionShazamResponse = new RecognitionShazamResponse();
            var xDocument = XDocument.Parse(responseString);
            XNamespace xNamespace = "http://orbit.shazam.com/v1/response";
            var elementIgnoreNamespace = xDocument.Root.GetElementIgnoreNamespace(xNamespace, "doRecognition1");
            var xElement = elementIgnoreNamespace;
            if (elementIgnoreNamespace == null)
            {
                var elementIgnoreNamespace1 = xDocument.Root.GetElementIgnoreNamespace(xNamespace, "error");
                var xElement1 = elementIgnoreNamespace1;
                if (elementIgnoreNamespace1 != null)
                    recognitionShazamResponse.errorCode = int.Parse(xElement1.Attribute("code").Value);
            }
            else
            {
                var elementIgnoreNamespace2 = xElement.GetElementIgnoreNamespace(xNamespace, "requestId");
                var xElement2 = elementIgnoreNamespace2;
                if (elementIgnoreNamespace2 != null)
                    recognitionShazamResponse.requestId = ulong.Parse(xElement2.Attribute("id").Value);
            }

            return recognitionShazamResponse;
        }

        private bool doTimeoutRequest(RequestContext requestContext, AsyncCallback callback, int millisecondsTimeout)
        {
            try
            {
                var flag = false;
                Timer timer = null;
                TimerCallback timerCallback = null;
                timerCallback = state =>
                {
                    lock (timer)
                    {
                        if (!flag)
                        {
                            flag = true;
                            requestContext.WebRequest.Abort();
                        }
                    }
                };
                AsyncCallback asyncCallback = ar =>
                {
                    lock (timer)
                    {
                        if (!flag)
                        {
                            flag = true;
                            timer.Change(-1, -1);
                        }
                    }

                    callback(ar);
                };
                if (!requestContext.WebRequest.Method.Equals("POST"))
                {
                    requestContext.WebRequest.BeginGetResponse(asyncCallback, requestContext);
                }
                else
                {
                    AsyncCallback asyncCallback1 = ar =>
                    {
                        requestContext = (RequestContext) ar.AsyncState;
                        using (var stream = requestContext.WebRequest.EndGetRequestStream(ar))
                        {
                            requestContext.RequestBuilder.WriteToRequestStream(stream);
                        }

                        requestContext.WebRequest.BeginGetResponse(asyncCallback, requestContext);
                    };
                    requestContext.WebRequest.BeginGetRequestStream(asyncCallback1, requestContext);
                }

                timer = new Timer(timerCallback, null, millisecondsTimeout, -1);
            }
            catch (Exception exception)
            {
                throw new IOException(string.Empty, exception);
            }

            return true;
        }

        private TrackVO ParseXmlElementForTrackData(XNamespace xNamespace, XElement trackElem, bool fromList = false)
        {
            var trackVO = new TrackVO();
            trackVO.Id = Convert.ToInt32(trackElem.Attribute("id").Value);
            trackVO.Title = trackElem.GetElementIgnoreNamespace(xNamespace, "ttitle").Value;
            var elementIgnoreNamespace = trackElem.GetElementIgnoreNamespace(xNamespace, "tartists");
            if (elementIgnoreNamespace != null)
            {
                var xElement = elementIgnoreNamespace.GetElementIgnoreNamespace(xNamespace, "tartist");
                if (xElement != null) trackVO.Artist = xElement.Value;
            }

            var elementIgnoreNamespace1 = trackElem.GetElementIgnoreNamespace(xNamespace, "tlabel");
            if (elementIgnoreNamespace1 != null) trackVO.Label = elementIgnoreNamespace1.Value;
            var xElement1 = trackElem.GetElementIgnoreNamespace(xNamespace, "tgenre");
            if (xElement1 != null)
            {
                var elementIgnoreNamespace2 = xElement1.GetElementIgnoreNamespace(xNamespace, "tparentGenre");
                if (elementIgnoreNamespace2 != null) trackVO.Genre = elementIgnoreNamespace2.Value;
            }

            var xElement2 = trackElem.GetElementIgnoreNamespace(xNamespace, "tcoverart");
            if (xElement2 != null) trackVO.ImageUri = xElement2.Value;
            var elementIgnoreNamespace3 = trackElem.GetElementIgnoreNamespace(xNamespace, "addOns");
            if (elementIgnoreNamespace3 != null)
                foreach (var xElement3 in elementIgnoreNamespace3.Elements(xNamespace + "addOn"))
                    if (xElement3.Attribute("providerName").Value == "Zune")
                    {
                        var elementIgnoreNamespace4 = xElement3.GetElementIgnoreNamespace(xNamespace, "actions");
                        if (elementIgnoreNamespace4 != null)
                        {
                            var xElement4 =
                                elementIgnoreNamespace4.GetElementIgnoreNamespace(xNamespace, "MarketplaceSearchTask");
                            if (xElement4 != null)
                            {
                                trackVO.ContentType = xElement4.Attribute("ContentType").Value;
                                trackVO.SearchTerms = xElement4.Attribute("SearchTerms").Value;
                            }
                        }

                        var elementIgnoreNamespace5 = xElement3.GetElementIgnoreNamespace(xNamespace, "content");
                        if (elementIgnoreNamespace5 == null) continue;
                        trackVO.PurchaseUrl = elementIgnoreNamespace5.Value;
                    }
                    else if (xElement3.Attribute("providerName").Value != "Share")
                    {
                        var xElement5 = xElement3.GetElementIgnoreNamespace(xNamespace, "actions");
                        if (xElement5 == null) continue;
                        var addOnVO = new AddOnVO();
                        addOnVO.ProviderName = xElement3.Attribute("providerName").Value;
                        addOnVO.Caption = xElement3.Attribute("typeName").Value;
                        var num = -1;
                        if (int.TryParse(xElement3.Attribute("typeId").Value, out num)) addOnVO.TypeId = num;
                        var num1 = -1;
                        if (xElement3.Attribute("creditTypeId") != null &&
                            int.TryParse(xElement3.Attribute("creditTypeId").Value, out num1))
                            addOnVO.CreditTypeId = num1;
                        addOnVO.Actions = new List<AddOnActionVO>();
                        foreach (var xElement6 in xElement5.Elements())
                        {
                            var addOnActionVO = new AddOnActionVO();
                            addOnActionVO.Url = xElement6.Attribute("Uri").Value;
                            var localName = xElement6.Name.LocalName;
                            var str = localName;
                            if (localName != null)
                                if (str == "LaunchUriTask")
                                    addOnActionVO.Type = AddOnActionVO.ActionType.LaunchUri;
                                else if (str == "WebViewTask") addOnActionVO.Type = AddOnActionVO.ActionType.WebView;
                            addOnVO.Actions.Add(addOnActionVO);
                        }

                        var providerName = addOnVO.ProviderName;
                        var str1 = providerName;
                        if (providerName != null)
                            switch (str1)
                            {
                                case "Buy":
                                {
                                    addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/buy.png";
                                    break;
                                }
                                case "YouTube":
                                {
                                    addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/youtube.png";
                                    break;
                                }
                                case "Biography":
                                {
                                    addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/biog.png";
                                    break;
                                }
                                case "Discography":
                                {
                                    addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/discog.png";
                                    break;
                                }
                                case "ProductReview":
                                {
                                    addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/reviews.png";
                                    break;
                                }
                                case "TrackReview":
                                {
                                    addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/trackreview.png";
                                    break;
                                }
                                case "ShazamLyrics":
                                {
                                    addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/lyrics.png";
                                    break;
                                }
                                case "Recommendations":
                                {
                                    addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/recommendations.png";
                                    break;
                                }
                            }
                        addOnVO.AssociateOwnerTrack(trackVO);
                        trackVO.AddOns = new List<AddOnVO>();
                        trackVO.AddOns.Add(addOnVO);
                    }
                    else
                    {
                        var elementIgnoreNamespace6 = xElement3.GetElementIgnoreNamespace(xNamespace, "actions");
                        if (elementIgnoreNamespace6 == null) continue;
                        var elementIgnoreNamespace7 =
                            elementIgnoreNamespace6.GetElementIgnoreNamespace(xNamespace, "ShareLinkTask");
                        if (elementIgnoreNamespace7 == null) continue;
                        trackVO.ShareLinkUri = elementIgnoreNamespace7.Attribute("LinkUri").Value;
                        trackVO.ShareLinkTitle = elementIgnoreNamespace7.Attribute("Title").Value;
                        trackVO.ShareLinkMessage = elementIgnoreNamespace7.Attribute("Message").Value;
                    }

            var xElement7 = trackElem.GetElementIgnoreNamespace(xNamespace, "tproduct");
            if (xElement7 != null) trackVO.Product = xElement7.Value;
            return trackVO;
        }
    }

    public class ShazamResponse
    {
        public ShazamResponse(ResultShazamResponse result)
        {
            if (result.newTag != null) Tag = result.newTag;
        }

        public ShazamResponse(Exception exception)
        {
            Exception = exception;
        }

        public TagVO Tag { get; }
        public Exception Exception { get; }
    }

    public enum MicrophoneRecordingOutputFormatType
    {
        PCM,
        MP3,
        SIG
    }

    public enum ShazamRecognitionState
    {
        Sending,
        Matching,
        Done,
        Failed
    }

    public delegate void ShazamStateChangedCallback(ShazamRecognitionState State, ShazamResponse Response);
}