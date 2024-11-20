-- todo:
-- figure out how to draw paths that go between rooms properly (track path children with hidden field?)
-- figure out how to force rerender rooms/entities to make sure sprites update when they need to

local drawableSprite = require("structs.drawable_sprite")
local drawableLine = require("structs.drawable_line")
local utils = require("utils")
local state = require("loaded_state")
local mods = require("mods")
local communalHelper = mods.requireFromPlugin("libraries.communal_helper")

local pathExtension = {}

pathExtension.name = "CommunalHelper/ShapeshifterPathExtension"
pathExtension.depth = -1000000
pathExtension.nodeLimits = { 2, 3 } -- set max node limit to higher than necessary so nodeAdded actually gets called
pathExtension.nodeVisibility = "never"
pathExtension.placements = {
    name = "shapeshifter_path_extension",
    data = {
        multiRoom = false,
        attachShapeshifters = true,
        parentId = 0,
    }
}
pathExtension.fieldInformation = {
    parentId = {
        fieldType = "integer",
        minimumValue = 0,
    }
}

local function findParent(currentRoom, entity)
    local roomsToSearch = entity.multiRoom and state.map.rooms or { currentRoom }
    local parent, parentFinalNode, parentIsPath, parentIsExtension

    for _, targetRoom in ipairs(roomsToSearch) do
        if targetRoom.entities then
            for _, e in ipairs(targetRoom.entities) do
                parentIsPath = e._name == "CommunalHelper/ShapeshifterPath"
                parentIsExtension = e._name == "CommunalHelper/ShapeshifterPathExtension"
                if (parentIsPath or parentIsExtension) and e._id == entity.parentId then
                    parent = e
                    local finalNode = parentIsPath and e.nodes[3] or e.nodes[2]
                    parentFinalNode = {
                        x = (finalNode.x or 0) + targetRoom.x - currentRoom.x,
                        y = (finalNode.y or 0) + targetRoom.y - currentRoom.y,
                    }
                    break
                end
            end
        end
    end

    return parent, parentFinalNode
end

local function dottify(lineSprites, onColor, offColor, patternWidth)
    local newSprites = {}
    for i, sprite in ipairs(lineSprites) do
        if math.fmod(math.floor(i / patternWidth), 2) == 0 then
            sprite:setColor(onColor)
        else
            sprite:setColor(offColor)
        end
        table.insert(newSprites, sprite)
    end
    return newSprites
end

local controlLineColor = { 1, 1, 1, 0.2 }
local cubicControlLineColor = { 0.5, 0.5, 0.5, 0.075 }
local controlNodeTexture = "particles/CommunalHelper/ring"
local arrowTexture = "particles/CommunalHelper/l"
local parentNotFoundTexture = "objects/CommunalHelper/shapeshifterPaths/info00"
local curveOnColor = { 1, 1, 1, 0.5 }
local curveOffColor = { 1, 1, 1, 0 }

function pathExtension.sprite(room, entity)
    local parent, parentFinalNode = findParent(room, entity)

    local x, y = entity.x or 0, entity.y or 0
    local points = {
        parentFinalNode or { x = 0, y = 0 },
        { x = x, y = y },
        table.unpack(entity.nodes or { { x = x + 16, y = y }, { x = x + 32, y = y } })
    }
    local a, ca, cb, b = table.unpack(points)

    local sprites = {}
    if not parent then
        table.insert(sprites, drawableSprite.fromTexture(parentNotFoundTexture, entity))
        return sprites
    end

    local function arrowAt(t)
        local mx, my = communalHelper.getCubicCurvePoint({ a.x, a.y }, { b.x, b.y }, { ca.x, ca.y }, { cb.x, cb.y }, t)
        local dx, dy = communalHelper.getCubicCurveDerivative({ a.x, a.y }, { b.x, b.y }, { ca.x, ca.y }, { cb.x, cb.y },
            t)
        local arrow = drawableSprite.fromTexture(arrowTexture, { x = mx, y = my })
        arrow.rotation = math.atan(dy / dx) + (dx >= 0 and 0 or math.pi) + math.pi / 4
        arrow:setColor(curveOnColor)
        table.insert(sprites, arrow)
    end

    table.insert(sprites, drawableSprite.fromTexture(controlNodeTexture, a))
    table.insert(sprites, drawableSprite.fromTexture(controlNodeTexture, ca))
    table.insert(sprites, drawableSprite.fromTexture(controlNodeTexture, cb))
    table.insert(sprites, drawableSprite.fromTexture(controlNodeTexture, b))

    local curve = drawableLine.fromPoints(communalHelper.getCubicCurve({ a.x, a.y }, { b.x, b.y }, { ca.x, ca.y },
        { cb.x, cb.y }, 32))
    for _, sprite in ipairs(dottify(curve:getDrawableSprite(), curveOnColor, curveOffColor, 2)) do
        table.insert(sprites, sprite)
    end

    arrowAt(0.25)
    arrowAt(0.50)
    arrowAt(0.75)

    table.insert(sprites, drawableLine.fromPoints({ a.x, a.y, ca.x + 0.5, ca.y + 0.5 }, controlLineColor))
    table.insert(sprites, drawableLine.fromPoints({ b.x, b.y, cb.x + 0.5, cb.y + 0.5 }, controlLineColor))
    table.insert(sprites,
        drawableLine.fromPoints({ ca.x + 0.5, ca.y + 0.5, cb.x + 0.5, cb.y + 0.5 }, cubicControlLineColor))

    return sprites
end

function pathExtension.selection(room, entity)
    local x, y = entity.x, entity.y
    local nodes = entity.nodes or { { x = x + 16, y = y }, { x = x + 32, y = y } }

    local nodeRectangles = {}
    for _, node in ipairs(nodes) do
        table.insert(nodeRectangles, utils.rectangle(node.x - 4, node.y - 4, 8, 8))
    end

    return utils.rectangle(x - 4, y - 4, 8, 8), nodeRectangles
end

-- always return false out of this, otherwise loenn assumes we added nodes and so tries to access ones that don't exist, resulting in a crash
function pathExtension.nodeAdded(room, entity, nodeIndex)
    local x, y = entity.nodes[2].x or 0, entity.nodes[2].y or 0

    for _, e in ipairs(room.entities) do
        if e._name == "CommunalHelper/ShapeshifterPathExtension" and e.parentId == entity._id then
            return false
        end
    end

    -- can't do it the "proper" way with placementUtils.placeItem as it would break in future loenn versions.
    table.insert(room.entities, {
        _type = "entity",
        _name = "CommunalHelper/ShapeshifterPathExtension",
        _id = communalHelper.nextAvailableId(),
        attachShapeshifters = true,
        multiRoom = false,
        nodes = {
            {
                x = x + 32,
                y = y
            },
            {
                x = x + 48,
                y = y
            }
        },
        parentId = entity._id,
        x = x + 16,
        y = y,
    })

    return false
end

return pathExtension
