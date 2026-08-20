using Windows.Security.Credentials;

namespace WinToastRelay.Services;

/// <summary>Stores the optional webhook bearer token in Windows Credential Manager, never in the JSON settings file.</summary>
public sealed class SecretStore
{
    private const string ResourceName = "WinToastRelay.Webhook";
    private const string UserName = "Authorization";

    public string Get()
    {
        try
        {
            var credential = new PasswordVault().Retrieve(ResourceName, UserName);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public void Save(string secret)
    {
        var vault = new PasswordVault();
        try { vault.Remove(vault.Retrieve(ResourceName, UserName)); }
        catch (Exception) { /* First run or no existing secret. */ }

        if (!string.IsNullOrWhiteSpace(secret)) vault.Add(new PasswordCredential(ResourceName, UserName, secret));
    }
}
