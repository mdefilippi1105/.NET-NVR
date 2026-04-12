using System.Collections;

namespace VideoRecorder.Util;

public class SharedData
{
    public static readonly Dictionary<string, string> ActiveStreams =  new Dictionary<string, string>();
    
    public static int StreamCount = 0;

    public static string ListStreams()
    {
        string data = "";
        
        foreach (KeyValuePair <string, string> kvp in ActiveStreams)
        {
          data  += "Stream List" + kvp.Key + ": " + kvp.Value +"\n";
            
        }
        return data;
    } 
}