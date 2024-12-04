using MonoMod.Utils;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CommunalHelper.MapDataProcessors;

// todo: move shapeshifter donotload to here ??? how
// umm also  fix offsets i think since we're loading from 1st level pos instead of (0,0)
// waitt i think i need to refactor the processing into the loadlevel hook as well cus otherwise the dictionaries are emptyy :(
// gravityhelper does it .  so
// if i'm doing that im gonna shove this into a class in the shapeshifter fileeee
public class ShapeshifterPathProcessor : EverestMapDataProcessor
{
    // dictionaries for merging shapeshifter paths

    // shapeshifterPaths[AreaSID][ModeID][parentPathID] = list of child extension ids
    private static Dictionary<string, List<Dictionary<int, List<int>>>> shapeshifterPaths = new();
    // childAdoptionPool[AreaSID][ModeID][parentID] = child it needs to adopt
    private static Dictionary<string, List<Dictionary<int, List<int>>>> childAdoptionPool = new();
    // shapeshifterPaths[AreaSID][ModeID][pathID] = (room element, path element)
    private static Dictionary<string, List<Dictionary<int, (BinaryPacker.Element, BinaryPacker.Element)>>> pathsByID = new();

    // dictionaries for loading entities immediately on map load

    // allGlobalPaths[AreaSID][ModeID] = list of global paths
    private static Dictionary<string, List<List<BinaryPacker.Element>>> allGlobalPaths = new();
    // allShapeshifters[AreaSID][ModeID] = list of shapeshifters
    private static Dictionary<string, List<List<BinaryPacker.Element>>> allShapeshifters = new();

    public override Dictionary<string, Action<BinaryPacker.Element>> Init()
    {
        Util.Log($"Initializing shapeshifter path processor for {AreaKey.SID} / {AreaKey.Mode}");

        string sid = AreaKey.SID;
        int mode = (int) AreaKey.Mode;

        BinaryPacker.Element currentRoom = new();
        void roomProcessor(BinaryPacker.Element room) => currentRoom = room;

        void shapeshifterProcessor(BinaryPacker.Element shapeshifter)
        {
            // clone the shapeshifter to not modify original
            BinaryPacker.Element newShapeshifter = new()
            {
                Package = shapeshifter.Package,
                Name = shapeshifter.Name,
                Attributes = new(shapeshifter.Attributes ?? new()),
                Children = new(shapeshifter.Children ?? new())
            };
            // since we are loading these on map load, they will not have the correct position data as they are not loaded with the room they were originally placed in.
            // so here, we set the x and y attributes to their current world position and when loading, we set the offset (room position) to 0, 0.
            newShapeshifter.SetAttr("x", newShapeshifter.AttrFloat("x") + currentRoom.AttrInt("x"));
            newShapeshifter.SetAttr("y", newShapeshifter.AttrFloat("y") + currentRoom.AttrInt("y"));

            allShapeshifters[sid][mode].Add(newShapeshifter);
        }

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
            {"entity:CommunalHelper/Shapeshifter", shapeshifterProcessor},
            {"entity:CommunalHelper/ShapeshifterPath",  shapeshifterPathProcessor},
            {"entity:CommunalHelper/ShapeshifterPathExtension",  shapeshifterPathExtensionProcessor},
        };
    }

    public override void Reset()
    {
        Util.Log($"Resetting shapeshifter path processor for {AreaKey.SID} / {AreaKey.Mode}");

        ResetMapDataDict(ref shapeshifterPaths);
        ResetMapDataDict(ref childAdoptionPool);
        ResetMapDataDict(ref pathsByID);

        ResetMapDataDict(ref allGlobalPaths);
        ResetMapDataDict(ref allShapeshifters);
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
            bool multiRoom = false;

            foreach ((int i, (BinaryPacker.Element childRoom, BinaryPacker.Element child)) in childIDs.Select((id, i) => (i, allPathsInMap[id])))
            {
                if (parentRoom != childRoom)
                {
                    multiRoom = true;
                }

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
            if (multiRoom)
            {
                parent.SetAttr("multiRoom", true);
                // since we are loading these on map load, they will not have the correct position data as they are not loaded with the room they were originally placed in.
                // so here, we set the x and y attributes to their current world position and when loading, we set the offset (room position) to 0, 0.
                parent.SetAttr("x", parent.AttrFloat("x") + parentRoomX);
                parent.SetAttr("y", parent.AttrFloat("y") + parentRoomY);
                foreach (BinaryPacker.Element node in parent.Children)
                {
                    node.SetAttr("x", node.AttrFloat("x") + parentRoomX);
                    node.SetAttr("y", node.AttrFloat("y") + parentRoomY);
                }
                allGlobalPaths[sid][mode].Add(parent);
            }
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

    #region Hooks

    internal static void Load()
    {
        On.Celeste.Level.LoadLevel += Level_LoadLevel;
    }

    internal static void Unload()
    {
        On.Celeste.Level.LoadLevel -= Level_LoadLevel;
    }

    // add all global paths and shapeshifters to the scene immediately on map load.
    // this is to allow shapeshifters attaching to multi-room paths without the player first loading the room the shapeshifter is in
    private static void Level_LoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader)
    {
        if (!isFromLoader)
        {
            orig(self, playerIntro, true);
            return;
        }

        string sid = self.Session.Area.SID;
        int mode = (int) self.Session.Area.Mode;

        foreach (BinaryPacker.Element path in allGlobalPaths[sid][mode])
        {
            EntityData data = CreateDataFromElement(self, path);
            // do not load this on the normal level load pass
            self.Session.DoNotLoad.Add(new EntityID(data.Level.Name, data.ID));
            Level.LoadCustomEntity(data, self);
        }

        foreach (BinaryPacker.Element shapeshifter in allShapeshifters[sid][mode])
        {
            shapeshifter.SetAttr("forceLoaded", true);
            EntityData data = CreateDataFromElement(self, shapeshifter);
            // Session.DoNotLoad logic handled in Shapeshifter.Added
            // wait i think Added is called after everything has been added, so there are  2 of them
            // bwuhh how do i DoNotLoad it hereee
            Level.LoadCustomEntity(data, self);
        }

        orig(self, playerIntro, true);
    }

    private static EntityData CreateDataFromElement(Level level, BinaryPacker.Element entity) =>
        DynamicData.For(level.Session.LevelData).Invoke<EntityData>("CreateEntityData", entity); // reflection :frowner:

    #endregion
}