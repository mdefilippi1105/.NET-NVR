namespace VideoRecorder.Services;

using System.Security.Cryptography;
using System.Text;


/*******************************************************************
 * This is a separate class to make the Digest Auth.
 * This class only has one job - take the username,
 * password, realm, nonce, method and URI
 * then we return the completed Auth header.
 * When Basic Auth fails...we use this.
 * Realm - is the name of the camera - like an ID that gets generated.
 * Nonce unique ID generated fresh by each 401 response
 *********************************************************************/
public class DigestAuth
{
    private static string Md5(string input)
    {
        //take a string and return a hex string
        byte[] bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        //convert the bytes into readable hex...lowercase required
        return Convert.ToHexString(bytes).ToLower();
    }

    public static string BuildHeader(string username, string password, string realm, string nonce, string method, string uri)
    {
        
        //step 1 - hash username:realm:password
        string hash1 = Md5($"{username}:{realm}:{password}");
        
        //step 2 - hash method:uri (eg DESCRIBE:rtsp://)
        string hash2 = Md5($"{method}:{uri}");
        
        //step 3 - hash ha1:nonce:ha2
        string response = Md5($"{hash1}:{nonce}:{hash2}");
        
        // build the final header string in the exact format the camera expects
        return $"Digest username=\"{username}\", realm=\"{realm}\", nonce=\"{nonce}\", uri=\"{uri}\", response=\"{response}\"";


    }
}