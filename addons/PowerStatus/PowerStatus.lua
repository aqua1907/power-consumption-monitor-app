local frame = CreateFrame("Frame", "PowerStatusFrame", UIParent)
frame:SetSize(228, 132)
frame:SetClampedToScreen(true)
frame:SetFrameStrata("MEDIUM")
frame:SetMovable(true)
frame:EnableMouse(true)
frame:RegisterForDrag("LeftButton")
local bg = frame:CreateTexture(nil, "BACKGROUND")
bg:SetAllPoints()
bg:SetColorTexture(0.04, 0.05, 0.06, 0.92)

local font, _, flags = GameFontNormal:GetFont()

local socText = frame:CreateFontString(nil, "OVERLAY")
socText:SetFont(font, 26, flags)
socText:SetPoint("TOPLEFT", 12, -10)
socText:SetJustifyH("LEFT")

local backupText = frame:CreateFontString(nil, "OVERLAY", "GameFontNormalLarge")
backupText:SetPoint("TOPRIGHT", -12, -16)
backupText:SetJustifyH("RIGHT")

local bar = CreateFrame("StatusBar", nil, frame)
bar:SetPoint("TOPLEFT", 12, -42)
bar:SetPoint("TOPRIGHT", -12, -42)
bar:SetHeight(8)
bar:SetStatusBarTexture("Interface\\TARGETINGFRAME\\UI-StatusBar")
bar:SetMinMaxValues(0, 100)
local barBg = bar:CreateTexture(nil, "BACKGROUND")
barBg:SetAllPoints()
barBg:SetColorTexture(0.15, 0.15, 0.15, 0.8)

local summaryText = frame:CreateFontString(nil, "OVERLAY", "GameFontHighlight")
summaryText:SetPoint("TOPLEFT", 12, -56)
summaryText:SetJustifyH("LEFT")

local flowText = frame:CreateFontString(nil, "OVERLAY", "GameFontHighlight")
flowText:SetPoint("TOPLEFT", 12, -74)
flowText:SetJustifyH("LEFT")

local loadText = frame:CreateFontString(nil, "OVERLAY", "GameFontDisable")
loadText:SetPoint("BOTTOMLEFT", 12, 18)
loadText:SetJustifyH("LEFT")

local updatedText = frame:CreateFontString(nil, "OVERLAY", "GameFontDisableSmall")
updatedText:SetPoint("BOTTOMLEFT", 12, 6)
updatedText:SetJustifyH("LEFT")

local function Place()
    frame:ClearAllPoints()
    local db = PowerStatusDB
    if db and db.point then
        frame:SetPoint(db.point, UIParent, db.relativePoint or db.point, db.x or 0, db.y or 0)
    else
        frame:SetPoint("TOP", UIParent, "TOP", 0, -80)
    end
end

local function Refresh()
    local feed = PowerStatus_Feed or {}
    if not feed.hasData then
        socText:SetText("--%")
        socText:SetTextColor(0.7, 0.7, 0.7)
        backupText:SetText("—")
        bar:SetValue(0)
        bar:SetStatusBarColor(0.4, 0.4, 0.4)
        summaryText:SetText(feed.summary or "No connection")
        summaryText:SetTextColor(1, 0.4, 0.35)
        flowText:SetText("")
        loadText:SetText(feed.error or "")
        updatedText:SetText(feed.updated or "")
        return
    end

    socText:SetText((feed.soc or 0) .. "%")
    backupText:SetText(feed.backup or "—")
    bar:SetValue(feed.soc or 0)
    if feed.low then
        socText:SetTextColor(1, 0.4, 0.35)
        bar:SetStatusBarColor(0.85, 0.28, 0.22)
    elseif feed.grid then
        socText:SetTextColor(0.9, 0.95, 0.9)
        bar:SetStatusBarColor(0.3, 0.72, 0.38)
    else
        socText:SetTextColor(1, 0.78, 0.4)
        bar:SetStatusBarColor(0.9, 0.62, 0.2)
    end

    summaryText:SetText(feed.summary or "")
    if feed.grid then
        summaryText:SetTextColor(0.75, 0.9, 0.75)
    else
        summaryText:SetTextColor(1, 0.7, 0.4)
    end
    flowText:SetText(feed.flow or "")
    flowText:SetTextColor(0.85, 0.85, 0.85)
    loadText:SetText(string.format("Home %s   PC %s", feed.load or "—", feed.pc or "—"))
    updatedText:SetText(feed.updated or "")
end

frame:SetScript("OnDragStart", frame.StartMoving)
frame:SetScript("OnDragStop", function(self)
    self:StopMovingOrSizing()
    local point, _, relativePoint, x, y = self:GetPoint(1)
    PowerStatusDB = PowerStatusDB or {}
    PowerStatusDB.point = point
    PowerStatusDB.relativePoint = relativePoint
    PowerStatusDB.x = x
    PowerStatusDB.y = y
end)

local function Toggle()
    frame:SetShown(not frame:IsShown())
    PowerStatusDB = PowerStatusDB or {}
    PowerStatusDB.hidden = not frame:IsShown()
end

SLASH_POWERSTATUS1 = "/ps"
SLASH_POWERSTATUS2 = "/powerstatus"
SlashCmdList.POWERSTATUS = Toggle

local button = CreateFrame("Button", "PowerStatusMinimapButton", Minimap)
button:SetSize(32, 32)
button:SetFrameStrata("MEDIUM")
button:SetFrameLevel(8)
button:RegisterForClicks("LeftButtonUp")
button:RegisterForDrag("LeftButton")

local icon = button:CreateTexture(nil, "ARTWORK")
icon:SetAllPoints()
icon:SetTexture("Interface\\AddOns\\PowerStatus\\icon")

local function Snap()
    local angle = (PowerStatusDB and PowerStatusDB.angle) or 210
    local rad = math.rad(angle)
    local radius = (Minimap:GetWidth() / 2) + 2
    button:ClearAllPoints()
    button:SetPoint("CENTER", Minimap, "CENTER", math.cos(rad) * radius, math.sin(rad) * radius)
end

local moved
button:SetScript("OnDragStart", function(self)
    moved = false
    self:SetScript("OnUpdate", function()
        moved = true
        local scale = Minimap:GetEffectiveScale()
        local cx, cy = GetCursorPosition()
        cx, cy = cx / scale, cy / scale
        local mx, my = Minimap:GetCenter()
        PowerStatusDB = PowerStatusDB or {}
        PowerStatusDB.angle = math.deg(math.atan2(cy - my, cx - mx))
        Snap()
    end)
end)
button:SetScript("OnDragStop", function(self)
    self:SetScript("OnUpdate", nil)
end)
button:SetScript("OnClick", function()
    if moved then
        moved = false
        return
    end
    Toggle()
end)

button:SetScript("OnEnter", function(self)
    local feed = PowerStatus_Feed or {}
    GameTooltip:SetOwner(self, "ANCHOR_LEFT")
    GameTooltip:AddLine("Power", 1, 0.82, 0)
    if feed.hasData then
        GameTooltip:AddDoubleLine("Battery", (feed.soc or 0) .. "%", 0.8, 0.8, 0.8, 1, 1, 1)
        GameTooltip:AddDoubleLine("Backup", feed.backup or "—", 0.8, 0.8, 0.8, 1, 1, 1)
        GameTooltip:AddLine(feed.summary or "", feed.grid and 0.6 or 1, feed.grid and 0.9 or 0.7, feed.grid and 0.6 or 0.4)
        if feed.flow and feed.flow ~= "" then
            GameTooltip:AddLine(feed.flow, 0.9, 0.9, 0.9)
        end
        GameTooltip:AddDoubleLine("Home", feed.load or "—", 0.7, 0.7, 0.7, 1, 1, 1)
        GameTooltip:AddDoubleLine("This PC", feed.pc or "—", 0.7, 0.7, 0.7, 1, 1, 1)
        if feed.updated and feed.updated ~= "" then
            GameTooltip:AddLine(feed.updated, 0.55, 0.55, 0.55)
        end
    else
        GameTooltip:AddLine(feed.summary or "No connection", 1, 0.4, 0.35)
        if feed.error and feed.error ~= "" then
            GameTooltip:AddLine(feed.error, 0.8, 0.5, 0.45)
        end
    end
    GameTooltip:Show()
end)
button:SetScript("OnLeave", function()
    GameTooltip:Hide()
end)

local events = CreateFrame("Frame")
events:RegisterEvent("PLAYER_LOGIN")
events:SetScript("OnEvent", function()
    Place()
    Snap()
    if PowerStatusDB and PowerStatusDB.hidden then
        frame:Hide()
    end
    Refresh()
end)
