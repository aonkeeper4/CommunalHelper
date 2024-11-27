using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CommunalHelper.MapDataProcessors;

public class ShapeshifterPathProcessor : EverestMapDataProcessor
{
    // shapeshifterPaths[AreaSID][ModeID][parentPathID] = list of child extension ids
    private static Dictionary<string, List<Dictionary<int, List<int>>>> shapeshifterPaths = new();
    // childAdoptionPool[AreaSID][ModeID][parentID] = child it needs to adopt
    private static Dictionary<string, List<Dictionary<int, List<int>>>> childAdoptionPool = new();
    // shapeshifterPaths[AreaSID][ModeID][pathID] = path element
    private static Dictionary<string, List<Dictionary<int, BinaryPacker.Element>>> pathsByID;

    public override Dictionary<string, Action<BinaryPacker.Element>> Init()
    {
        Util.Log($"Initializing shapeshifter path processor for {AreaKey.SID} / {AreaKey.Mode}");

        void shapeshifterPathProcessor(BinaryPacker.Element shapeshifterPath)
        {
            string sid = AreaKey.SID;
            int mode = (int) AreaKey.Mode;
            int id = shapeshifterPath.AttrInt("id");

            Dictionary<int, BinaryPacker.Element> allPathsInMap = pathsByID[sid][mode];
            if (!allPathsInMap.ContainsKey(id))
            {
                allPathsInMap[id] = shapeshifterPath;
            }
            else
            {
                throw new Exception($"got ShapeshifterPath with same id as one already seen: {id}");
            }

            Dictionary<int, List<int>> allParentsInMap = shapeshifterPaths[sid][mode];
            allParentsInMap[id] = new();

            Dictionary<int, List<int>> adoptionPool = childAdoptionPool[sid][mode];
            List<int> adopted = new();
            foreach ((int parent, List<int> childrenToAdopt) in adoptionPool)
            {
                if (parent == id)
                {
                    if (adopted.Count > 0)
                    {
                        Util.Log($"Path segment id {id} has a child already! Discarding children with ids {string.Join(", ", childrenToAdopt)}.");
                    }
                    else
                    {
                        foreach (int child in childrenToAdopt)
                        {
                            allParentsInMap[id].Add(child);
                        }
                        adopted = childrenToAdopt;
                    }
                }
            }
            foreach (int child in adopted)
            {
                adoptionPool.Remove(child);
            }
        }

        void shapeshifterPathExtensionProcessor(BinaryPacker.Element shapeshifterPathExtension)
        {
            string sid = AreaKey.SID;
            int mode = (int) AreaKey.Mode;
            int selfID = shapeshifterPathExtension.AttrInt("id"), parentID = shapeshifterPathExtension.AttrInt("parentId");

            Dictionary<int, BinaryPacker.Element> allPathsInMap = pathsByID[sid][mode];
            if (!allPathsInMap.ContainsKey(selfID))
            {
                allPathsInMap[selfID] = shapeshifterPathExtension;
            }
            else
            {
                throw new Exception($"got ShapeshifterPathExtension with same id as one already seen: {selfID}");
            }

            Dictionary<int, List<int>> allParentsInMap = shapeshifterPaths[sid][mode];
            Dictionary<int, List<int>> adoptionPool = childAdoptionPool[sid][mode];
            int myTopLevelParent = -1;
            foreach ((int topLevelParent, List<int> children) in allParentsInMap)
            {
                if (children.Contains(parentID) || topLevelParent == parentID)
                {
                    allParentsInMap[topLevelParent].Add(selfID);
                    myTopLevelParent = topLevelParent;
                }
            }
            if (myTopLevelParent == -1)
            {
                adoptionPool[parentID] = new() { selfID };
            }

            List<int> adoptees = new();
            List<int> myOrphanedChildren = new();
            foreach ((int parent, List<int> childrenToAdopt) in adoptionPool)
            {
                if (parent == selfID)
                {
                    if (adoptees.Count > 0 || myOrphanedChildren.Count > 0)
                    {
                        Util.Log($"Path segment id {selfID} has a child already! Discarding children with ids {string.Join(", ", childrenToAdopt)}.");
                    }
                    else if (myTopLevelParent != -1)
                    {
                        foreach (int child in childrenToAdopt)
                        {
                            allParentsInMap[myTopLevelParent].Add(child);
                        }
                        adoptees = childrenToAdopt;
                    }
                    else
                    {
                        foreach (int child in childrenToAdopt)
                        {
                            myOrphanedChildren.Add(child);
                        }
                    }
                }
            }
            foreach (int orphan in myOrphanedChildren)
            {
                adoptionPool[selfID].Add(orphan);
            }
            foreach (int adoptee in adoptees)
            {
                adoptionPool.Remove(adoptee);
            }
        }

        return new Dictionary<string, Action<BinaryPacker.Element>>() {
            {"entity:CommunalHelper/ShapeshifterPath",  shapeshifterPathProcessor},
            {"entity:CommunalHelper/ShapeshifterPathExtension",  shapeshifterPathExtensionProcessor}
        };
    }

    public override void End()
    {
        // todo: merge all parent-child dictionaries into single ShapeshifterPaths containing all nodes
        Util.Log($"Merging shapeshifter paths for {AreaKey.SID} / {AreaKey.Mode}");

        string sid = AreaKey.SID;
        int mode = (int) AreaKey.Mode;

        Dictionary<int, List<int>> allParentsInMap = shapeshifterPaths[sid][mode];
        Dictionary<int, BinaryPacker.Element> allPathsInMap = pathsByID[sid][mode];
        foreach ((int parentID, List<int> childIDs) in allParentsInMap)
        {
            BinaryPacker.Element parent = allPathsInMap[parentID];
            foreach (BinaryPacker.Element childNode in childIDs.SelectMany(id => allPathsInMap[id].Children))
            {
                parent.Children.Add(childNode);
            }
        }

        shapeshifterPaths.Clear();
        childAdoptionPool.Clear();
        pathsByID.Clear();
    }

    private void ResetMapDataDict<T>(ref Dictionary<string, List<T>> dict) where T : new()
    {
        if (!dict.ContainsKey(AreaKey.SID))
        {
            // create an entry for the current map SID.
            dict[AreaKey.SID] = new();
        }
        while (dict[AreaKey.SID].Count <= (int) AreaKey.Mode)
        {
            // fill out the empty space before the current map MODE with empty dictionaries.
            dict[AreaKey.SID].Add(new());
        }

        // reset the dictionary for the current map and mode.
        dict[AreaKey.SID][(int) AreaKey.Mode] = new();
    }

    public override void Reset()
    {
        Util.Log($"Resetting shapeshifter path processor for {AreaKey.SID} / {AreaKey.Mode}");

        ResetMapDataDict(ref shapeshifterPaths);
        ResetMapDataDict(ref childAdoptionPool);
        ResetMapDataDict(ref pathsByID);
    }
}