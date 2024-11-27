using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CommunalHelper.MapDataProcessors;

public class ShapeshifterPathProcessor : EverestMapDataProcessor
{
    // shapeshifterPaths[AreaSID][ModeID][parentPathID] = list of child extension ids
    private static Dictionary<string, List<Dictionary<int, List<int>>>> shapeshifterPaths = new();
    // childAdoptionPool[AreaSID][ModeID][parentID] = child it needs to adopt
    private static Dictionary<string, List<Dictionary<int, List<int>>>> childAdoptionPool = new();
    // shapeshifterPaths[AreaSID][ModeID][pathID] = (room element, path element)
    private static Dictionary<string, List<Dictionary<int, (BinaryPacker.Element, BinaryPacker.Element)>>> pathsByID = new();

    public override Dictionary<string, Action<BinaryPacker.Element>> Init()
    {
        Util.Log($"Initializing shapeshifter path processor for {AreaKey.SID} / {AreaKey.Mode}");

        string sid = AreaKey.SID;
        int mode = (int) AreaKey.Mode;

        BinaryPacker.Element currentRoom = new();
        void roomProcessor(BinaryPacker.Element room) => currentRoom = room;

        void shapeshifterPathProcessor(BinaryPacker.Element shapeshifterPath)
        {
            int id = shapeshifterPath.AttrInt("id");

            Dictionary<int, (BinaryPacker.Element, BinaryPacker.Element)> allPathsInMap = pathsByID[sid][mode];
            if (!allPathsInMap.ContainsKey(id))
            {
                allPathsInMap[id] = (currentRoom, shapeshifterPath);
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
            int selfID = shapeshifterPathExtension.AttrInt("id"), parentID = shapeshifterPathExtension.AttrInt("parentId");

            Dictionary<int, (BinaryPacker.Element, BinaryPacker.Element)> allPathsInMap = pathsByID[sid][mode];
            if (!allPathsInMap.ContainsKey(selfID))
            {
                allPathsInMap[selfID] = (currentRoom, shapeshifterPathExtension);
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
            {"level", roomProcessor},
            {"entity:CommunalHelper/ShapeshifterPath",  shapeshifterPathProcessor},
            {"entity:CommunalHelper/ShapeshifterPathExtension",  shapeshifterPathExtensionProcessor}
        };
    }

    public override void Reset()
    {
        Util.Log($"Resetting shapeshifter path processor for {AreaKey.SID} / {AreaKey.Mode}");

        ResetMapDataDict(ref shapeshifterPaths);
        ResetMapDataDict(ref childAdoptionPool);
        ResetMapDataDict(ref pathsByID);
    }

    private void ResetMapDataDict<T>(ref Dictionary<string, List<T>> dict) where T : new()
    {
        string sid = AreaKey.SID;
        int mode = (int) AreaKey.Mode;

        if (!dict.ContainsKey(sid))
        {
            dict[sid] = new();
        }
        while (dict[sid].Count <= mode)
        {
            dict[sid].Add(new());
        }
        dict[sid][mode] = new();
    }

    public override void End()
    {
        Util.Log($"Merging shapeshifter paths for {AreaKey.SID} / {AreaKey.Mode}");

        string sid = AreaKey.SID;
        int mode = (int) AreaKey.Mode;

        Dictionary<int, List<int>> allParentsInMap = shapeshifterPaths[sid][mode];
        Dictionary<int, (BinaryPacker.Element, BinaryPacker.Element)> allPathsInMap = pathsByID[sid][mode];
        foreach ((int parentID, List<int> childIDs) in allParentsInMap)
        {
            List<int> attachIndices = new() { 0 };
            (BinaryPacker.Element parentRoom, BinaryPacker.Element parent) = allPathsInMap[parentID];
            int parentRoomX = parentRoom.AttrInt("x"), parentRoomY = parentRoom.AttrInt("y");

            foreach ((int i, (BinaryPacker.Element childRoom, BinaryPacker.Element child)) in childIDs.Select((id, i) => (i, allPathsInMap[id])))
            {
                int childRoomX = childRoom.AttrInt("x"), childRoomY = childRoom.AttrInt("y");

                if (child.AttrBool("attachShapeshifters"))
                {
                    attachIndices.Add(i + 1);
                }

                float childX = child.AttrFloat("x"), childY = child.AttrFloat("y");
                parent.Children.Add(MakeNode(childX + childRoomX - parentRoomX, childY + childRoomY - parentRoomY));

                foreach (BinaryPacker.Element node in child.Children)
                {
                    float nodeX = node.AttrFloat("x"), nodeY = node.AttrFloat("y");
                    parent.Children.Add(MakeNode(nodeX + childRoomX - parentRoomX, nodeY + childRoomY - parentRoomY));
                }
            }

            parent.SetAttr("shapeshifterAttachIndices", string.Join(",", attachIndices));
        }
    }

    private static BinaryPacker.Element MakeNode(float x, float y)
    {
        return new()
        {
            Package = "",
            Name = "node",
            Attributes = new() {
                {"x", x},
                {"y", y}
            }
        };
    }
}