-- Online lobby browser.
--
-- The screen only gathers information and negotiates a network path; the actual
-- handshake is performed by the caller in main.lua, because the function that
-- enters the synced netplay menu is local to that file.

local lobby = {}

local function f_url()
	local u = gameOption('Netplay.LobbyURL')
	if u == nil or u == '' then
		return 'http://127.0.0.1:8080'
	end
	return u
end

local function f_name()
	local n = gameOption('Netplay.LobbyName')
	if n == nil or n == '' then
		return 'Player'
	end
	return n
end

local function f_key(name)
	local m = motif.title_info.menu
	if m ~= nil and m[name] ~= nil and m[name].key ~= nil then
		return getInput(-1, m[name].key)
	end
	return false
end

local function f_line(txt, y)
	local ts = motif.title_info.connecting.TextSpriteData
	textImgReset(ts)
	textImgSetText(ts, txt)
	textImgSetPos(ts, 20, y)
	textImgDraw(ts)
end

local function f_begin()
	clearColor(
		motif[main.background].bgclearcolor[1],
		motif[main.background].bgclearcolor[2],
		motif[main.background].bgclearcolor[3]
	)
	bgDraw(motif[main.background].BGDef, 0)
	rectDraw(motif.title_info.connecting.overlay.RectData)
end

local function f_finish()
	bgDraw(motif[main.background].BGDef, 1)
	refresh()
end

-- Waits for hole punching (and the relay fallback) to settle before the caller
-- starts the handshake, so the match never begins on a dead path.
local function f_negotiate(m)
	lobbyEstablishPath()
	local frames = 0
	while frames < 60 * 12 do
		frames = frames + 1
		local mode = lobbyNatMode()
		if mode ~= '' then
			m.natMode = mode
			return m
		end
		local st = lobbyStatus()
		if st.error ~= '' and not st.busy then
			f_begin()
			f_line('CONNECTION FAILED', 90)
			f_line(st.error, 110)
			f_line('PRESS ESC', 140)
			f_finish()
			if esc() or f_key('cancel') then
				return nil
			end
		else
			f_begin()
			f_line('NEGOTIATING CONNECTION...', 90)
			f_line('OPPONENT: ' .. (m.peerName or '?'), 110)
			f_finish()
		end
		if esc() then
			return nil
		end
	end
	return nil
end

local function f_drawRooms(rooms, cursor, note)
	f_begin()
	f_line('ONLINE LOBBY - ' .. #rooms .. ' ROOM(S)', 20)
	f_line('YOUR ADDRESS: ' .. lobbyLocalAddr(), 36)
	if #rooms == 0 then
		f_line('NO ROOMS YET - PRESS C TO CREATE ONE', 70)
	else
		for i = 1, #rooms do
			local r = rooms[i]
			local mark = '  '
			if i == cursor then
				mark = '> '
			end
			local watchers = ''
			if (r.spectators or 0) > 0 then
				watchers = '  ' .. r.spectators .. ' WATCHING'
			end
			f_line(
				mark .. r.name .. '  [' .. r.hostName .. ']  ' ..
				r.players .. '/' .. r.capacity .. '  ' .. r.state .. watchers,
				60 + (i - 1) * 14
			)
		end
	end
	f_line('UP/DOWN=SELECT  ENTER=JOIN  S=WATCH  C=CREATE  ESC=BACK', 200)
	if note ~= '' then
		f_line(note, 220)
	end
	f_finish()
end

-- Watchers need to load the same fight the players picked. The host publishes
-- that once the select screen has settled, which is exactly when launchFight
-- has resolved both rosters and the stage.
--
-- Character references are roster indices, so a watcher whose select.def
-- differs from the host's will load the wrong fighters. The players already
-- guard against that with the content fingerprint; spectators do not.
lobby.hosting = false

hook.add('launchFight', 'lobby.publishManifest', function(common, t, data)
	if not lobby.hosting then
		return
	end
	local p1 = start.p[1].t_selected[1]
	local p2 = start.p[2].t_selected[1]
	if p1 == nil or p2 == nil then
		return
	end
	lobbyPublishManifest({
		p1 = p1.ref,
		p1pal = p1.pal or 1,
		p2 = p2.ref,
		p2pal = p2.pal or 1,
		stage = t.stageNo or getStageNo(),
		time = t.roundtime or -1,
	})
end)

-- Runs the match a watcher is following. There is no local input: every frame
-- comes from the host through the spectator backend.
function lobby.f_spectate(m)
	local manifest = nil
	local frames = 0
	while manifest == nil do
		frames = frames + 1
		if frames > 60 * 60 then
			return false
		end
		manifest = lobbyManifest()
		f_begin()
		f_line('WAITING FOR THE PLAYERS TO PICK...', 90)
		f_line('HOST: ' .. (m.peerName or '?'), 110)
		f_line('ESC=STOP WATCHING', 140)
		f_finish()
		if esc() or f_key('cancel') then
			return false
		end
	end

	local ok, err = pcall(lobbyEnterSpectate)
	if not ok then
		f_begin()
		f_line('COULD NOT WATCH', 90)
		f_line(tostring(err), 110)
		f_finish()
		return false
	end

	setGameMode('versus')
	setTeamMode(1, 'single', 1)
	setTeamMode(2, 'single', 1)
	selectChar(1, manifest.p1, manifest.p1pal)
	selectChar(2, manifest.p2, manifest.p2pal)
	selectStage(manifest.stage)
	if (manifest.time or -1) >= 0 then
		setRoundTime(manifest.time)
	end
	loadStart('vsscreen=false, victoryscreen=false, winscreen=false, continue=false')
	start.f_game({lua = {}})
	return true
end

-- Returns the match table once a path to the opponent exists, or nil if the
-- player backed out.
function lobby.f_browser()
	lobbyConnect(f_url(), f_name(), gameOption('Netplay.ListenPort'))

	local cursor = 1
	local note = ''

	while true do
		local st = lobbyStatus()
		if st.error ~= '' then
			note = st.error
		end

		local m = lobbyMatch()
		if m ~= nil and m.ready then
			return f_negotiate(m)
		end

		if not st.connected then
			f_begin()
			f_line('CONNECTING TO LOBBY', 90)
			f_line(f_url(), 110)
			if note ~= '' then
				f_line(note, 140)
			end
			f_line('ESC=CANCEL', 170)
			f_finish()
			if esc() or f_key('cancel') then
				lobbyDisconnect()
				return nil
			end
		elseif m ~= nil then
			f_begin()
			f_line('ROOM: ' .. (m.roomName or ''), 60)
			if m.role == 'spectator' then
				f_line('WAITING FOR THE MATCH TO START...', 80)
			else
				f_line('WAITING FOR AN OPPONENT...', 80)
			end
			f_line('YOUR ADDRESS: ' .. lobbyLocalAddr(), 100)
			f_line('ESC=LEAVE ROOM', 140)
			f_finish()
			if esc() or f_key('cancel') then
				lobbyLeaveRoom()
			end
		else
			local rooms = lobbyRooms()
			if cursor > #rooms then
				cursor = math.max(1, #rooms)
			end
			f_drawRooms(rooms, cursor, note)

			if esc() or f_key('cancel') then
				lobbyDisconnect()
				return nil
			elseif getKey('c') then
				note = ''
				lobbyCreateRoom(f_name() .. "'S ROOM")
			elseif getKey('s') and #rooms > 0 then
				note = ''
				lobbySpectateRoom(rooms[cursor].id)
			elseif f_key('previous') and cursor > 1 then
				cursor = cursor - 1
			elseif f_key('next') and cursor < #rooms then
				cursor = cursor + 1
			elseif f_key('done') and #rooms > 0 then
				note = ''
				lobbyJoinRoom(rooms[cursor].id)
			end
		end
	end
end

return lobby
