using System.Collections.Generic;

namespace MagiCommon.Models
{
    public class ElasticFolder : ElasticObject
    {
        // TODO: I was gonna have the folder know about its children but if I'm gonna query against the parentId anyway then it's just extraneous data

        //public HashSet<string> ChildFiles { get; set; } = new HashSet<string>();
        //public HashSet<string> ChildFolders { get; set; } = new HashSet<string>();
    }
}
