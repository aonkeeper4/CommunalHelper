local utils = require("utils")
local state = require("loaded_state")

local shapeshifterUtils = {}

function shapeshifterUtils.findParent(currentRoom, entity)
    local roomsToSearch = entity.multiRoom and state.map.rooms or { currentRoom }
    local parent, parentFinalNode, parentIsPath, parentIsExtension, parentRoom

    for _, targetRoom in ipairs(roomsToSearch) do
        if targetRoom.entities then
            for _, e in ipairs(targetRoom.entities) do
                parentIsPath = e._name == "CommunalHelper/ShapeshifterPath"
                parentIsExtension = e._name == "CommunalHelper/ShapeshifterPathExtension"
                if (parentIsPath or parentIsExtension) and e._id == entity.parentId then
                    parent = e
                    local finalNode = e.nodes[parentIsPath and 3 or 2]
                    parentFinalNode = {
                        x = (finalNode.x or e.x - 16) + targetRoom.x - currentRoom.x,
                        y = (finalNode.y or e.y) + targetRoom.y - currentRoom.y,
                    }
                    parentRoom = targetRoom
                    break
                end
            end
        end
    end

    return parent, parentFinalNode, parentRoom
end

function shapeshifterUtils.findChild(currentRoom, entity)
    local child, childRoom

    for _, targetRoom in ipairs(state.map.rooms) do
        if targetRoom.entities then
            for _, e in ipairs(targetRoom.entities) do
                if e._name == "CommunalHelper/ShapeshifterPathExtension" and e.parentId == entity._id and e.multiRoom then
                    child = utils.deepcopy(e)
                    childRoom = targetRoom

                    child.x = child.x + childRoom.x - currentRoom.x
                    child.y = child.y + childRoom.y - currentRoom.y
                    for _, node in ipairs(child.nodes) do
                        node.x = node.x + childRoom.x - currentRoom.x
                        node.y = node.y + childRoom.y - currentRoom.y
                    end

                    break
                end
            end
        end
    end

    return child, childRoom
end

return shapeshifterUtils
