-- todo:
-- performance :oshiregret2:

local drawableSprite = require("structs.drawable_sprite")
local drawableLine = require("structs.drawable_line")
local utils = require("utils")
local toolUtils = require("tool_utils")
local state = require("loaded_state")
local celesteRender = require("celeste_render")
local mods = require("mods")
local communalHelper = mods.requireFromPlugin("libraries.communal_helper")
local shapeshifterUtils = mods.requireFromPlugin("libraries.shapeshifter_utils")

local pathExtension = {}

pathExtension.name = "CommunalHelper/ShapeshifterPathExtension"
pathExtension.depth = -1000000
pathExtension.nodeLimits = { 2, 3 } -- set max node limit to higher than necessary so nodeAdded gets called
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

function pathExtension.sprite(room, entity, viewport, parentOverride)
    local parent, parentFinalNode
    if parentOverride then
        parent = parentOverride
        parentFinalNode = parentOverride.nodes[parentOverride._name == "CommunalHelper/ShapeshifterPath" and 3 or 2]
    else
        parent, parentFinalNode = shapeshifterUtils.findParent(room, entity)
    end

    local x, y = entity.x or 0, entity.y or 0
    local points = {
        parentFinalNode or { x = x - 16, y = y },
        { x = x, y = y },
        table.unpack(entity.nodes or { { x = x + 16, y = y }, { x = x + 32, y = y } })
    }
    local a, ca, cb, b = table.unpack(points)

    local sprites = {}

    for _, p in ipairs(points) do
        table.insert(sprites, drawableSprite.fromTexture(controlNodeTexture, p))
    end

    table.insert(sprites, drawableLine.fromPoints({ a.x, a.y, ca.x + 0.5, ca.y + 0.5 }, controlLineColor))
    table.insert(sprites, drawableLine.fromPoints({ b.x, b.y, cb.x + 0.5, cb.y + 0.5 }, controlLineColor))
    table.insert(sprites,
        drawableLine.fromPoints({ ca.x + 0.5, ca.y + 0.5, cb.x + 0.5, cb.y + 0.5 }, cubicControlLineColor))

    if not parent then
        table.insert(sprites, drawableSprite.fromTexture(parentNotFoundTexture, { x = entity.x, y = entity.y + 8 }))
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

    local curve = drawableLine.fromPoints(communalHelper.getCubicCurve({ a.x, a.y }, { b.x, b.y }, { ca.x, ca.y },
        { cb.x, cb.y }, 32))
    for _, sprite in ipairs(dottify(curve:getDrawableSprite(), curveOnColor, curveOffColor, 2)) do
        table.insert(sprites, sprite)
    end

    arrowAt(0.25)
    arrowAt(0.50)
    arrowAt(0.75)

    local child, childRoom = shapeshifterUtils.findChild(room, entity)
    if child then
        for _, sprite in ipairs(pathExtension.sprite(childRoom, child, viewport, entity)) do
            table.insert(sprites, sprite)
        end
    end

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

-- always return false from this, otherwise loenn assumes we added nodes and so tries to access ones that don't exist, resulting in a crash
function pathExtension.nodeAdded(room, entity, nodeIndex)
    local x, y = entity.x or 0, entity.y or 0
    local nodes = entity.nodes or { { x = x + 16, y = y }, { x = x + 32, y = y } }
    local nx, ny = nodes[2].x or x + 32, entity.nodes[2].y or y

    local child, _ = shapeshifterUtils.findChild(room, entity)
    if child then return false end

    -- can't do it the "proper" way with placementUtils.placeItem as it would break in future loenn versions
    table.insert(room.entities, {
        _type = "entity",
        _name = "CommunalHelper/ShapeshifterPathExtension",
        _id = communalHelper.nextAvailableId(),
        attachShapeshifters = true,
        multiRoom = false,
        nodes = {
            {
                x = nx + 32,
                y = ny
            },
            {
                x = nx + 48,
                y = ny
            }
        },
        parentId = entity._id,
        x = nx + 16,
        y = ny,
    })
    -- i'm a bit worried about the performance of this but uhhh we'll see
    toolUtils.redrawTargetLayer(room, "entities")
    return false
end

function pathExtension.move(room, entity, nodeIndex, offsetX, offsetY)
    -- default move behavior
    if nodeIndex == 0 then
        entity.x = entity.x + offsetX
        entity.y = entity.y + offsetY
    else
        local nodes = entity.nodes

        if nodes and nodeIndex <= #nodes then
            local target = nodes[nodeIndex]

            target.x = target.x + offsetX
            target.y = target.y + offsetY
        end
    end

    -- this gets the parent and child rooms to update visually even if they're deselected.
    -- i'm a bit worried about the performance of this but uhhh we'll see
    -- ooouh  yeah you can feel itt
    local parent, _, parentRoom = shapeshifterUtils.findParent(room, entity)
    if parent and not utils.equals(parentRoom, room) then
        celesteRender.forceRedrawRoom(parentRoom, state, false)
    end

    local child, childRoom = shapeshifterUtils.findChild(room, entity)
    if child then
        celesteRender.forceRedrawRoom(childRoom, state, false)
    end
end

return pathExtension
