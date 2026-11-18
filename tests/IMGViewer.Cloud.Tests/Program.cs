using System.IO;
using IMGViewer.Cloud;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Xml;
internal static class Program
{
    static readonly R2Settings Settings=new(true,"https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com","sample-bucket","fake-access","fake-secret","img-viewer/images/","",false);
    static int checks;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
    static void Reject(Action action){try{action();}catch(ArgumentException){checks++;return;}throw new Exception("Invalid configuration/key accepted");}
    [STAThread] static int Main()
    {
        try {Run().GetAwaiter().GetResult();Console.WriteLine($"PASS: {checks} checks — signing, private/public links, paging, scope, upload, download, deletion, errors, cancellation and Windows-protected credentials. No live R2 calls.");return 0;}
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    static async Task Run()
    {
        var client=new R2Client(Settings);
        var date=new DateTimeOffset(2026,11,17,12,0,0,TimeSpan.Zero);
        using(var request=client.Sign(HttpMethod.Put,"img-viewer/images/prueba é +#.png",Encoding.UTF8.GetBytes("hello"),"image/png",now:date))
        {
            Check(request.RequestUri!.AbsolutePath.EndsWith("prueba%20%C3%A9%20%2B%23.png"),"Unicode key escaped incorrectly");
            Check(request.Headers.GetValues("Authorization").Single().EndsWith("Signature=4538ad05aff371f73dd442e87df806142c6fd4db65cb521b60ff8f95a0ba4c0e"),"SigV4 disagrees with independent fixture");
        }
        Reject(()=>new R2Client(Settings with {Endpoint="https://r2.cloudflarestorage.com.evil.example"}));
        Reject(()=>new R2Client(Settings with {Endpoint="http://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com"}));
        Reject(()=>new R2Client(Settings with {Prefix="../downloads/"}));Reject(()=>new R2Client(Settings with {Prefix=""}));
        Reject(()=>client.CheckKey("img-viewer/downloads/IMGViewer.exe"));Reject(()=>client.CheckKey("img-viewer/images/../downloads/latest.json"));
        Reject(()=>client.CheckKey("img-viewer/images2/other.jpg"));Reject(()=>new R2Client(Settings with {PublicUrl="https://user:password@example.com"}));
        string link=client.Link("img-viewer/images/a b.png",date);
        Check(link.Contains("X-Amz-Expires=3600")&&link.Contains("X-Amz-Signature=")&&!link.Contains("fake-secret"),"Private link missing signature, expiry or exposes secret");
        string publicLink=new R2Client(Settings with {PublicUrl="https://cdn.example.com"}).Link("img-viewer/images/a b.png",date);
        Check(publicLink=="https://cdn.example.com/img-viewer/images/a%20b.png","Public link incorrect");
        int page=0,puts=0,deletes=0;string? objectKey=null;
        var handler=new FakeHandler(async(request,token)=>{
            Check(request.Headers.Contains("Authorization"),"Unsigned HTTP operation");
            if(request.Method==HttpMethod.Get && request.RequestUri!.Query.Contains("list-type"))
            {
                page++;Check(request.RequestUri.Query.Contains("prefix=img-viewer%2Fimages%2F"),"Listing omitted scoped prefix");
                if(page==2)Check(request.RequestUri.Query.Contains("continuation-token=a%2Bb%2F%3D"),"Continuation token not encoded");
                string xml=page==1?"<ListBucketResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\"><Contents><Key>img-viewer/images/a.png</Key><Size>4</Size><LastModified>2026-11-17T12:00:00Z</LastModified></Contents><Contents><Key>img-viewer/images/ignore.exe</Key><Size>4</Size></Contents><Contents><Key>other/private.jpg</Key><Size>4</Size></Contents><NextContinuationToken>a+b/=</NextContinuationToken></ListBucketResult>":"<ListBucketResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\"><Contents><Key>img-viewer/images/b.jpg</Key><Size>4</Size></Contents></ListBucketResult>";
                return new(HttpStatusCode.OK){Content=new StringContent(xml)};
            }
            if(request.Method==HttpMethod.Put){puts++;objectKey=Uri.UnescapeDataString(request.RequestUri!.AbsolutePath).Replace("/sample-bucket/","");Check((await request.Content!.ReadAsByteArrayAsync(token)).SequenceEqual(new byte[]{1,2,3,4}),"Upload payload changed");Check(request.Content.Headers.ContentType!.MediaType=="image/png","Wrong content type");return new(HttpStatusCode.OK);}
            if(request.Method==HttpMethod.Delete){deletes++;return new(HttpStatusCode.NoContent);}
            return new(HttpStatusCode.OK){Content=new ByteArrayContent([1,2,3,4])};
        });
        using var http=new HttpClient(handler);var mock=new R2Client(Settings,http);
        var first=await mock.List(null,CancellationToken.None);Check(first.Items.Count==1&&first.Next=="a+b/=","List filtering/pagination incorrect");
        var second=await mock.List(first.Next,CancellationToken.None);Check(second.Items.Single().Name=="b.jpg"&&second.Next is null,"Second page incorrect");
        string uploaded=await mock.Upload([1,2,3,4],"a.png",null,CancellationToken.None);Check(puts==1&&uploaded==objectKey&&uploaded.StartsWith(Settings.Prefix),"Upload key not scoped/unique");
        Check((await mock.Get(uploaded,CancellationToken.None)).SequenceEqual(new byte[]{1,2,3,4}),"Downloaded bytes changed");await mock.Delete(uploaded,CancellationToken.None);Check(deletes==1,"Delete not issued");
        using var forbiddenHttp=new HttpClient(new FakeHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))));
        try{await new R2Client(Settings,forbiddenHttp).List(null,CancellationToken.None);throw new Exception("403 swallowed");}catch(HttpRequestException e){Check(e.StatusCode==HttpStatusCode.Forbidden&&!e.Message.Contains("fake-secret"),"Unsafe/incomplete error");}
        using var cts=new CancellationTokenSource();cts.Cancel();
        try{await mock.Get(uploaded,cts.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){checks++;}
        using var largeHttp=new HttpClient(new FakeHandler((_,_)=>{var response=new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent([])};response.Content.Headers.ContentLength=R2Client.MaximumImageBytes+1L;return Task.FromResult(response);}));
        try{await new R2Client(Settings,largeHttp).Get(uploaded,CancellationToken.None);throw new Exception("Oversized image accepted");}catch(InvalidOperationException){checks++;}
        using var xmlHttp=new HttpClient(new FakeHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("<!DOCTYPE x [<!ENTITY leak SYSTEM 'file:///not-read'>]><x>&leak;</x>")})));
        try{await new R2Client(Settings,xmlHttp).List(null,CancellationToken.None);throw new Exception("External entity accepted");}catch(XmlException){checks++;}
        string temp=Path.Combine(Path.GetTempPath(),"IMGViewer-cloud-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);string config=Path.Combine(temp,"connection.dat");
        try{
            R2Preferences.SaveTo(config,Settings);var encrypted=File.ReadAllBytes(config);Check(!Encoding.UTF8.GetString(encrypted).Contains("fake-secret")&&!Encoding.UTF8.GetString(encrypted).Contains("fake-access"),"Credentials stored as plaintext");
            Check(R2Preferences.LoadFrom(config)==Settings,"Protected settings round-trip failed");
            R2Preferences.SaveTo(config,Settings with {Enabled=false});Check(!R2Preferences.LoadFrom(config).Enabled,"Disable not persisted");Check(Directory.GetFiles(temp,"*.tmp").Length==0,"Temporary credential file leaked");
        }finally{if(File.Exists(config))File.Delete(config);Directory.Delete(temp);}
    }
    private sealed class FakeHandler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return send(request,token);}
    }
}
