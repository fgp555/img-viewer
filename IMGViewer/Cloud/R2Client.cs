using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
namespace IMGViewer.Cloud;
internal sealed record R2Object(string Key,long Size,DateTimeOffset Modified)
{
    public string Name=>Path.GetFileName(Key);
}
internal sealed record R2Page(IReadOnlyList<R2Object> Items,string? Next);
internal sealed class R2Client
{
    internal const int MaximumImageBytes=128*1024*1024;
    private static readonly HttpClient Shared=new(new HttpClientHandler {AllowAutoRedirect=false}) {Timeout=TimeSpan.FromMinutes(3)};
    private readonly HttpClient http;
    internal readonly R2Settings Settings;
    internal R2Client(R2Settings settings,HttpClient? client=null) {Settings=settings.Validate();http=client??Shared;}
    private static string Encode(string value)=>Uri.EscapeDataString(value);
    private static string Hash(byte[] value)=>Convert.ToHexStringLower(SHA256.HashData(value));
    private static byte[] Hmac(byte[] key,string value)=>HMACSHA256.HashData(key,Encoding.UTF8.GetBytes(value));
    private byte[] SigningKey(string day)=>Hmac(Hmac(Hmac(Hmac(Encoding.UTF8.GetBytes("AWS4"+Settings.SecretKey),day),"auto"),"s3"),"aws4_request");
    private static string Query(IEnumerable<KeyValuePair<string,string>> values)=>string.Join("&",values.Select(v=>(Key:Encode(v.Key),Value:Encode(v.Value))).OrderBy(v=>v.Key,StringComparer.Ordinal).ThenBy(v=>v.Value,StringComparer.Ordinal).Select(v=>v.Key+"="+v.Value));
    private string ObjectPath(string? key)
    {
        if(key is not null)CheckKey(key);
        return "/"+Encode(Settings.Bucket)+(key is null?"":"/"+string.Join("/",key.Split('/').Select(Encode)));
    }
    internal void CheckKey(string key)
    {
        if(!key.StartsWith(Settings.Prefix,StringComparison.Ordinal)||key.Length<=Settings.Prefix.Length||key.Contains('\\')||key.Any(char.IsControl)||key.Split('/').Any(s=>s is "." or ".." or ""))throw new ArgumentException("La imagen debe pertenecer a la carpeta remota configurada.");
    }
    internal HttpRequestMessage Sign(HttpMethod method,string? key,byte[]? body=null,string? type=null,IEnumerable<KeyValuePair<string,string>>? query=null,DateTimeOffset? now=null,IProgress<double>? progress=null)
    {
        var date=(now??DateTimeOffset.UtcNow).UtcDateTime;string stamp=date.ToString("yyyyMMddTHHmmssZ",CultureInfo.InvariantCulture),day=stamp[..8];
        string path=ObjectPath(key),qs=Query(query??[]),hash=Hash(body??[]);
        var url=new Uri(Settings.Endpoint+path+(qs.Length>0?"?"+qs:""));
        var headers=new SortedDictionary<string,string>(StringComparer.Ordinal) {{"host",url.Authority},{"x-amz-content-sha256",hash},{"x-amz-date",stamp}};
        if(type is not null)headers["content-type"]=type;
        string signed=string.Join(";",headers.Keys),scope=day+"/auto/s3/aws4_request";
        string canonical=method.Method+"\n"+path+"\n"+qs+"\n"+string.Concat(headers.Select(h=>h.Key+":"+h.Value+"\n"))+"\n"+signed+"\n"+hash;
        string signature=Convert.ToHexStringLower(Hmac(SigningKey(day),"AWS4-HMAC-SHA256\n"+stamp+"\n"+scope+"\n"+Hash(Encoding.UTF8.GetBytes(canonical))));
        var request=new HttpRequestMessage(method,url);
        request.Headers.TryAddWithoutValidation("x-amz-date",stamp);request.Headers.TryAddWithoutValidation("x-amz-content-sha256",hash);
        request.Headers.TryAddWithoutValidation("Authorization",$"AWS4-HMAC-SHA256 Credential={Settings.AccessKey}/{scope}, SignedHeaders={signed}, Signature={signature}");
        if(body is not null) {request.Content=new UploadContent(body,progress);if(type is not null)request.Content.Headers.ContentType=new MediaTypeHeaderValue(type);}
        return request;
    }
    internal string Link(string key,DateTimeOffset? now=null)
    {
        CheckKey(key);
        if(Settings.PublicUrl.Length>0)return Settings.PublicUrl+string.Join("/",key.Split('/').Select(Encode));
        var date=(now??DateTimeOffset.UtcNow).UtcDateTime;string stamp=date.ToString("yyyyMMddTHHmmssZ",CultureInfo.InvariantCulture),day=stamp[..8],scope=day+"/auto/s3/aws4_request";
        var args=new Dictionary<string,string> {{"X-Amz-Algorithm","AWS4-HMAC-SHA256"},{"X-Amz-Credential",Settings.AccessKey+"/"+scope},{"X-Amz-Date",stamp},{"X-Amz-Expires","3600"},{"X-Amz-SignedHeaders","host"}};
        string path=ObjectPath(key),qs=Query(args),host=new Uri(Settings.Endpoint).Authority;
        string canonical="GET\n"+path+"\n"+qs+"\nhost:"+host+"\n\nhost\nUNSIGNED-PAYLOAD";
        string sig=Convert.ToHexStringLower(Hmac(SigningKey(day),"AWS4-HMAC-SHA256\n"+stamp+"\n"+scope+"\n"+Hash(Encoding.UTF8.GetBytes(canonical))));
        return Settings.Endpoint+path+"?"+qs+"&X-Amz-Signature="+sig;
    }
    internal async Task<R2Page> List(string? continuation,CancellationToken token)
    {
        var query=new Dictionary<string,string> {{"list-type","2"},{"prefix",Settings.Prefix},{"max-keys","50"}};
        if(continuation is not null)query["continuation-token"]=continuation;
        using var request=Sign(HttpMethod.Get,null,query:query);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);Ensure(response);
        await using var stream=await response.Content.ReadAsStreamAsync(token);
        using var reader=XmlReader.Create(stream,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2_000_000,Async=true});
        var xml=await XDocument.LoadAsync(reader,LoadOptions.None,token);XNamespace ns=xml.Root?.Name.Namespace??"";
        var items=new List<R2Object>();
        foreach(var item in xml.Root?.Elements(ns+"Contents")??[])
        {
            string key=(string?)item.Element(ns+"Key")??"";
            if(!key.StartsWith(Settings.Prefix,StringComparison.Ordinal)||key.EndsWith('/'))continue;
            CheckKey(key);
            if(!IsImage(key))continue;
            long.TryParse((string?)item.Element(ns+"Size"),NumberStyles.None,CultureInfo.InvariantCulture,out long size);
            DateTimeOffset.TryParse((string?)item.Element(ns+"LastModified"),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var modified);
            items.Add(new(key,size,modified));
        }
        string? next=(string?)xml.Root?.Element(ns+"NextContinuationToken");
        return new(items,string.IsNullOrEmpty(next)?null:next);
    }
    internal static bool IsImage(string key)=>Path.GetExtension(key).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".svg";
    internal async Task<string> Upload(byte[] bytes,string name,IProgress<double>? progress,CancellationToken token)
    {
        if(bytes.Length>MaximumImageBytes)throw new InvalidOperationException("La imagen supera el límite de 128 MB por subida.");
        string extension=Path.GetExtension(name).ToLowerInvariant();if(!IsImage(name))throw new ArgumentException("Formato de imagen no disponible.");
        string stem=Path.GetFileNameWithoutExtension(name);stem=new string(stem.Where(c=>char.IsLetterOrDigit(c)||c is '-' or '_' or ' ').Take(70).ToArray());if(stem.Length==0)stem="imagen";
        string key=Settings.Prefix+stem+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N")+extension;
        string type=extension switch {".jpg" or ".jpeg"=>"image/jpeg",".png"=>"image/png",".webp"=>"image/webp",".bmp"=>"image/bmp",".svg"=>"image/svg+xml",_=>"application/octet-stream"};
        using var request=Sign(HttpMethod.Put,key,bytes,type,progress:progress);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);Ensure(response);progress?.Report(1);return key;
    }
    internal async Task<byte[]> Get(string key,CancellationToken token)
    {
        using var request=Sign(HttpMethod.Get,key);using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);Ensure(response);
        if(response.Content.Headers.ContentLength>MaximumImageBytes)throw new InvalidOperationException("La imagen supera los 128 MB.");
        await using var stream=await response.Content.ReadAsStreamAsync(token);using var output=new MemoryStream();var buffer=new byte[65536];int count;
        while((count=await stream.ReadAsync(buffer,token))>0){if(output.Length+count>MaximumImageBytes)throw new InvalidOperationException("La imagen supera los 128 MB.");await output.WriteAsync(buffer.AsMemory(0,count),token);}
        return output.ToArray();
    }
    internal async Task Delete(string key,CancellationToken token)
    {
        using var request=Sign(HttpMethod.Delete,key);using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);Ensure(response);
    }
    private static void Ensure(HttpResponseMessage response)
    {
        if(response.IsSuccessStatusCode)return;
        string reason=response.StatusCode switch {HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized=>"Revisa las credenciales y los permisos Object Read & Write del bucket.",HttpStatusCode.NotFound=>"No se encontró el bucket o la imagen.",_=>"Inténtalo de nuevo."};
        throw new HttpRequestException($"R2 respondió HTTP {(int)response.StatusCode}. {reason}",null,response.StatusCode);
    }
    private sealed class UploadContent(byte[] data,IProgress<double>? progress):HttpContent
    {
        protected override bool TryComputeLength(out long length){length=data.Length;return true;}
        protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context)=>Write(stream,CancellationToken.None);
        protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context,CancellationToken token)=>Write(stream,token);
        private async Task Write(Stream stream,CancellationToken token)
        {
            for(int offset=0;offset<data.Length;offset+=65536){int count=Math.Min(65536,data.Length-offset);await stream.WriteAsync(data.AsMemory(offset,count),token);progress?.Report((offset+count)/(double)data.Length);}
        }
    }
}
