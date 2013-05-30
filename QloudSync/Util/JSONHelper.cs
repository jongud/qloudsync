using System;
using Newtonsoft.Json.Linq;
using System.IO;
using Amazon.S3.Model;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Threading;
using Newtonsoft;


using GreenQloud.Model;
using GreenQloud.Repository;
using GreenQloud.Util;
using GreenQloud.Persistence;
using GreenQloud.Repository.Local;
using GreenQloud.Repository.Remote;


namespace GreenQloud
{
    public class JSONHelper
    {
        public static JObject GetInfo (string url)
        {
            System.Net.WebRequest myReq = System.Net.WebRequest.Create (url);
            string receiveContent = string.Empty;
            try {
                using (System.Net.WebResponse wr = myReq.GetResponse ()) {
                    Stream receiveStream = wr.GetResponseStream ();
                    StreamReader reader = new StreamReader (receiveStream, System.Text.Encoding.UTF8);
                    receiveContent = reader.ReadToEnd ();
                    
                }
                
                return Newtonsoft.Json.Linq.JObject.Parse(receiveContent);
            } catch (Exception e){
                Console.WriteLine(e.Data);
                return new JObject();
            }
        }

        public static JArray GetInfoArray (string url)
        {
            System.Net.WebRequest myReq = System.Net.WebRequest.Create (url);
            string receiveContent = string.Empty;
            try {
                using (System.Net.WebResponse wr = myReq.GetResponse ()) {
                    Stream receiveStream = wr.GetResponseStream ();
                    StreamReader reader = new StreamReader (receiveStream, System.Text.Encoding.UTF8);
                    receiveContent = reader.ReadToEnd ();
                    
                }
                
                return Newtonsoft.Json.Linq.JArray.Parse(receiveContent);
            } catch (Exception e){
                Console.WriteLine(e.Data);
                throw new DisconnectionException();
            }
        }


        public string postJSON (Event e)
        {

            string hash = Crypto.GetHMACbase64(Credential.SecretKey,Credential.PublicKey, true);
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(string.Format("https://my.greenqloud.com/qloudsync/history/{0}?username={1}&hash={2}",RuntimeSettings.DefaultBucketName, Credential.Username, hash));
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            string json = "";
            string result = null;

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {


                json = "{\"action\":\""+e.EventType+"\"," +
                    "\"application\":\""+GlobalSettings.FullApplicationName+"\"," + "\"applicationVersion\":\""+GlobalSettings.RunningVersion+"\"," + "\"bucket\":\""+Credential.Username+""+GlobalSettings.SuffixNameBucket+"\"," + "\"deviceId\":\""+GlobalSettings.DeviceIdHash+"\"," + "\"hash\":\""+e.Item.LocalMD5Hash+"\"," + "\"object\":\"" + e.Item.FullRemoteName +"\"," + "\"os\":\""+GlobalSettings.OSVersion+"\"," + "\"resultObject\":\""+e.ResultObject+"\"," + "\"username\":\""+Credential.Username+"\"}";

                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();

            }
            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {

                result = streamReader.ReadToEnd();


            }
            return result;

        }
    }
}
