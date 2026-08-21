using System.Security.Cryptography;
using System.Text;
using MimeKit;

namespace MailService.Infrastructure.Security.Dkim;

public class DkimVerifier
{
    public string Verify(byte[] emlBytes, string? dkimPublicKey)
    {
        if (string.IsNullOrWhiteSpace(dkimPublicKey))
        {
            return "None";
        }

        try
        {
            using var stream = new MemoryStream(emlBytes);
            var message = MimeMessage.Load(stream);

            var dkimHeader = message.Headers.FirstOrDefault(h => h.Id == HeaderId.DkimSignature);
            if (dkimHeader == null)
            {
                return "None";
            }

            // Extract public key data (p= parameter)
            string pValue = "";
            foreach (var part in dkimPublicKey.Split(';'))
            {
                var kv = part.Trim().Split('=', 2);
                if (kv.Length == 2 && kv[0].Trim().Equals("p", StringComparison.OrdinalIgnoreCase))
                {
                    pValue = kv[1].Trim();
                    break;
                }
            }

            if (string.IsNullOrEmpty(pValue))
            {
                return "None";
            }

            // Parse RSA public key from Base64
            byte[] keyBytes = Convert.FromBase64String(pValue);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);

            return "Pass";
        }
        catch
        {
            return "Fail";
        }
    }
}
