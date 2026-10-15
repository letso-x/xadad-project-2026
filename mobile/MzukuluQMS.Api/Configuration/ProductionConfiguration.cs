using Npgsql;
namespace MzukuluQMS.Api.Configuration;

public static class ProductionConfiguration
{
    // Fail closed without ever echoing values into startup diagnostics.
    public static void Validate(IConfiguration configuration)
    {
        var raw=configuration.GetConnectionString("QmsDatabase");
        if(string.IsNullOrWhiteSpace(raw))throw new InvalidOperationException("ConnectionStrings:QmsDatabase is required in Production.");
        NpgsqlConnectionStringBuilder db;
        try{db=new NpgsqlConnectionStringBuilder(raw);}
        catch(ArgumentException){throw new InvalidOperationException("ConnectionStrings:QmsDatabase has an invalid format.");}
        if(string.IsNullOrWhiteSpace(db.Host)||string.IsNullOrWhiteSpace(db.Database)||string.IsNullOrWhiteSpace(db.Username))
            throw new InvalidOperationException("ConnectionStrings:QmsDatabase requires Host, Database and Username.");
        if(db.SslMode!=SslMode.VerifyFull)
            throw new InvalidOperationException("ConnectionStrings:QmsDatabase must use SSL Mode=VerifyFull in Production.");
        if(!string.IsNullOrWhiteSpace(db.RootCertificate)&&!File.Exists(db.RootCertificate))
            throw new InvalidOperationException("The configured database Root Certificate file is unavailable.");
        if(!Uri.TryCreate(configuration["SupabaseStorage:BaseUrl"],UriKind.Absolute,out var storage)||storage.Scheme!="https"||!string.IsNullOrEmpty(storage.UserInfo)||!string.IsNullOrEmpty(storage.Query))
            throw new InvalidOperationException("SupabaseStorage:BaseUrl must be an absolute HTTPS URL without credentials or a query.");
        foreach(var key in new[]{"SupabaseStorage:SecretKey","SupabaseStorage:Bucket"})
            if(string.IsNullOrWhiteSpace(configuration[key]))throw new InvalidOperationException(key+" is required in Production.");
    }
}
