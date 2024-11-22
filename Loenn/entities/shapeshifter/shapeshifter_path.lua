local drawableSprite = require("structs.drawable_sprite")
local drawableLine = require("structs.drawable_line")
local utils = require("utils")
local toolUtils = require("tool_utils")
local state = require("loaded_state")
local celesteRender = require("celeste_render")
local entities = require("entities")
local mods = require("mods")
local communalHelper = mods.requireFromPlugin("libraries.communal_helper")
local shapeshifterUtils = mods.requireFromPlugin("libraries.shapeshifter_utils")

local path = {}

path.name = "CommunalHelper/ShapeshifterPath"
path.depth = -1000000
path.nodeLimits = { 3, 4 } -- set max node limit to higher than necessary so nodeAdded actually gets called
path.nodeVisibility = "never"

path.fieldInformation = {
    duration = {
        fieldType = "number",
        minimumValue = 0.0
    },
    easer = {
        editable = false,
        options = communalHelper.easers,
    },
    rotateYaw = {
        fieldType = "integer",
    },
    rotatePitch = {
        fieldType = "integer",
    },
    rotateRoll = {
        fieldType = "integer",
    },
    quakeTime = {
        fieldType = "number",
        minimumValue = 0.0,
    },
    fakeoutTime = {
        fieldType = "number",
        minimumValue = 0.0,
    },
    fakeoutDistance = {
        fieldType = "number",
        minimumValue = 0.0,
    }
}

path.placements = {
    name = "shapeshiter_path",
    data = {
        duration = 2.0,
        easer = "Linear",
        rotateYaw = 0,
        rotatePitch = 0,
        rotateRoll = 0,
        quakeTime = 0.5,
        fakeoutTime = 0.75,
        fakeoutDistance = 32.0
    }
}

local controlLineColor = { 1, 1, 1, 0.2 }
local cubicControlLineColor = { 0.5, 0.5, 0.5, 0.075 }
local controlNodeTexture = "particles/CommunalHelper/ring"
local arrowTexture = "particles/CommunalHelper/l"

function path.sprite(room, entity, viewport)
    local x, y = entity.x or 0, entity.y or 0
    local nodes = entity.nodes or { { x = x + 16, y = y }, { x = x + 32, y = y }, { x = x + 48, y = y } }

    local count = #nodes

    local points = { { x = x, y = y } }
    for i = 1, count do
        table.insert(points, nodes[i])
    end

    local sprites = {}

    table.insert(sprites, drawableSprite.fromTexture(controlNodeTexture, entity))

    local a = points[1]
    local ca = points[2]
    local cb = points[3]
    local b = points[4]

    local function arrowAt(t)
        local mx, my = communalHelper.getCubicCurvePoint({ a.x, a.y }, { b.x, b.y }, { ca.x, ca.y }, { cb.x, cb.y }, t)
        local dx, dy = communalHelper.getCubicCurveDerivative({ a.x, a.y }, { b.x, b.y }, { ca.x, ca.y }, { cb.x, cb.y },
            t)
        local arrow = drawableSprite.fromTexture(arrowTexture, { x = mx, y = my })
        arrow.rotation = math.atan(dy / dx) + (dx >= 0 and 0 or math.pi) + math.pi / 4
        table.insert(sprites, arrow)
    end

    table.insert(sprites, drawableSprite.fromTexture(controlNodeTexture, ca))
    table.insert(sprites, drawableSprite.fromTexture(controlNodeTexture, cb))
    table.insert(sprites, drawableSprite.fromTexture(controlNodeTexture, b))

    table.insert(sprites,
        drawableLine.fromPoints(communalHelper.getCubicCurve({ a.x, a.y }, { b.x, b.y }, { ca.x, ca.y }, { cb.x, cb.y },
            32)))

    arrowAt(0.25)
    arrowAt(0.50)
    arrowAt(0.75)

    table.insert(sprites, drawableLine.fromPoints({ a.x, a.y, ca.x + 0.5, ca.y + 0.5 }, controlLineColor))
    table.insert(sprites, drawableLine.fromPoints({ b.x, b.y, cb.x + 0.5, cb.y + 0.5 }, controlLineColor))
    table.insert(sprites,
        drawableLine.fromPoints({ ca.x + 0.5, ca.y + 0.5, cb.x + 0.5, cb.y + 0.5 }, cubicControlLineColor))

    local child, childRoom = shapeshifterUtils.findChild(room, entity)
    local pathExtensionHandler = entities.registeredEntities["CommunalHelper/ShapeshifterPathExtension"]
    if child then
        for _, sprite in ipairs(pathExtensionHandler.sprite(childRoom, child, viewport, entity)) do
            table.insert(sprites, sprite)
        end
    end

    return sprites
end

function path.selection(room, entity)
    local x, y = entity.x, entity.y
    local nodes = entity.nodes or { { x = x + 16, y = y }, { x = x + 32, y = y }, { x = x + 48, y = y } }

    local nodeRectangles = {}
    for _, node in ipairs(nodes) do
        table.insert(nodeRectangles, utils.rectangle(node.x - 4, node.y - 4, 8, 8))
    end

    return utils.rectangle(x - 4, y - 4, 8, 8), nodeRectangles
end

-- always return false from this, otherwise loenn assumes we added nodes and so tries to access ones that don't exist, resulting in a crash
function path.nodeAdded(room, entity, nodeIndex)
    local x, y = entity.x or 0, entity.y or 0
    local nodes = entity.nodes or { { x = x + 16, y = y }, { x = x + 32, y = y }, { x = x + 48, y = y } }
    local nx, ny = nodes[3].x or x + 48, entity.nodes[3].y or y

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

function path.move(room, entity, nodeIndex, offsetX, offsetY)
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
    local child, childRoom = shapeshifterUtils.findChild(room, entity)
    if child then
        celesteRender.forceRedrawRoom(childRoom, state, false)
    end
end

return path
