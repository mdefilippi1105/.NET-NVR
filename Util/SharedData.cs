using System.Collections;

namespace VideoRecorder.Util;

public class SharedData
{
    public static readonly Dictionary<string, string> ActiveStreams =  new Dictionary<string, string>();
    
    public static int StreamCount = 0;
    
}