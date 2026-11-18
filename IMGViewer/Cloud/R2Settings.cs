using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace IMGViewer.Cloud;
internal sealed record R2Settings(bool Enabled=false,string Endpoint="",string Bucket="",string AccessKey="",string SecretKey="",string Prefix="img-viewer/images/",string PublicUrl="",bool CopyLink=true)
{
    public override string ToString()=>"Cloudflare R2 (credenciales protegidas)";
    internal R2Settings Validate()
    {
        if(!Uri.TryCreate(Endpoint.Trim(),UriKind.Absolute,out var endpoint) || endpoint.Scheme!="https" || !endpoint.Host.EndsWith(".r2.cloudflarestorage.com",StringComparison.OrdinalIgnoreCase) || endpoint.UserInfo!="" || endpoint.AbsolutePath!="/" || endpoint.Query!="" || endpoint.Fragment!="" || !endpoint.IsDefaultPort)
            throw new ArgumentException("Introduce el endpoint S3 HTTPS de R2, sin bucket ni carpeta.");
        if(!Regex.IsMatch(Bucket.Trim(),"^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$"))throw new ArgumentException("Revisa el nombre del bucket.");
        if(string.IsNullOrWhiteSpace(AccessKey)||string.IsNullOrWhiteSpace(SecretKey)||AccessKey.Any(char.IsControl)||SecretKey.Any(char.IsControl))throw new ArgumentException("Introduce Access Key ID y Secret Access Key de R2.");
        string prefix=Prefix.Trim().Trim('/');
        if(prefix.Length==0 || prefix.Split('/').Any(p=>p is "" or "." or "..") || prefix.Contains('\\') || prefix.Any(char.IsControl))throw new ArgumentException("Introduce una carpeta remota, por ejemplo img-viewer/images/.");
        string publicUrl=PublicUrl.Trim();
        if(publicUrl.Length>0 && (!Uri.TryCreate(publicUrl,UriKind.Absolute,out var url)||url.Scheme!="https"||url.UserInfo!=""||url.Query!=""||url.Fragment!=""))throw new ArgumentException("El dominio público debe ser una URL HTTPS sin parámetros.");
        return this with {Endpoint=endpoint.GetLeftPart(UriPartial.Authority),Bucket=Bucket.Trim(),AccessKey=AccessKey.Trim(),SecretKey=SecretKey.Trim(),Prefix=prefix+"/",PublicUrl=publicUrl.Length==0?"":publicUrl.TrimEnd('/')+"/"};
    }
}
internal static class R2Preferences
{
    private static readonly string PathName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IMGViewer","cloud-r2.dat");
    internal static R2Settings Load()
    {
        if(!File.Exists(PathName))return new();
        byte[] plain=Protect(File.ReadAllBytes(PathName),false);
        try {return JsonSerializer.Deserialize<R2Settings>(plain)??new();}
        finally {CryptographicOperations.ZeroMemory(plain);}
    }
    internal static void Save(R2Settings settings)=>SaveTo(PathName,settings);
    internal static void SaveTo(string path,R2Settings settings)
    {
        byte[] plain=JsonSerializer.SerializeToUtf8Bytes(settings);
        byte[] encrypted;
        try {encrypted=Protect(plain,true);}finally{CryptographicOperations.ZeroMemory(plain);}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {File.WriteAllBytes(temporary,encrypted);File.Move(temporary,path,true);}
        finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
    internal static R2Settings LoadFrom(string path)
    {
        byte[] plain=Protect(File.ReadAllBytes(path),false);
        try {return JsonSerializer.Deserialize<R2Settings>(plain)??new();}finally{CryptographicOperations.ZeroMemory(plain);}
    }
    private static byte[] Protect(byte[] bytes,bool encrypt)
    {
        var input=new Blob {Length=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};Blob output=default;
        try
        {
            Marshal.Copy(bytes,0,input.Data,bytes.Length);
            bool success=encrypt?CryptProtectData(ref input,null,0,0,0,1,out output):CryptUnprotectData(ref input,0,0,0,0,1,out output);
            if(!success)throw new Win32Exception(Marshal.GetLastWin32Error());
            var result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;
        }
        finally
        {
            Marshal.Copy(new byte[bytes.Length],0,input.Data,bytes.Length);Marshal.FreeHGlobal(input.Data);
            if(output.Data!=0){Marshal.Copy(new byte[output.Length],0,output.Data,output.Length);LocalFree(output.Data);}
        }
    }
    [StructLayout(LayoutKind.Sequential)]private struct Blob {public int Length;public nint Data;}
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool CryptProtectData(ref Blob input,string? description,nint entropy,nint reserved,nint prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true)]private static extern bool CryptUnprotectData(ref Blob input,nint description,nint entropy,nint reserved,nint prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")]private static extern nint LocalFree(nint memory);
}
