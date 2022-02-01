# from https://github.com/ziadoma/dota_hoster

from steam.client import SteamClient
from steam.enums import EResult
from dota2.client import Dota2Client
from dota2.features.chat import ChannelManager
import dota2
import config
import os
import socket
import threading
import shlex
import logging
from dota2.enums import DOTAChatChannelType_t, EDOTAGCMsg, DOTA_GameState, DOTA_GameMode, EServerRegion

print("Starting lobby hoster...", flush=True)

client = SteamClient()
dota = Dota2Client(client)
dota.verbose_debug = True
logging.basicConfig(format="%(name)s: %(message)s", level=logging.DEBUG)

admins = config.admins
lobby_slots = [""] * 10

isReady = False
draftStarted = False
matchEnded = False

sock = None

def update_slot(lobby):
    for slot in range(10):
        for member in lobby.all_members:
            if slot < 5:
                team = 0  # DOTA_GC_TEAM_GOOD_GUYS (Radiant)
            else:
                team = 1  # DOTA_GC_TEAM_BAD_GUYS (Dire)
            if member.team == team and member.slot == slot + 1 - team * 5:
                lobby_slots[slot] = member.name
            else:
                lobby_slots[slot] = ""
    print(lobby_slots, flush=True)


def check_to_start(lobby):
    if lobby is not None:
        if '' in lobby_slots:
            return
        if lobby.team_details:
            if lobby.game_mode == 2:
                if lobby.team_details[0].team_name != "" and lobby.team_details[1].team_name != "":
                    start_lobby()
            else:
                start_lobby()


def balance_lobby():
    dota.balanced_shuffle_lobby()


def flip_lobby():
    dota.flip_lobby_teams()


def kick_player(player_id):
    dota.practice_lobby_kick(player_id)

def coin_flip():
    dota.send(EDOTAGCMsg.EMsgSelectionPriorityChoiceRequest, {})

def start_lobby():
    playerInLobby = False
    for p in lobby_slots:
        if p != '':
            playerInLobby = True
            break

    if playerInLobby:
        print("Starting game...", flush=True)
        dota.launch_practice_lobby()
    else:
        sock.sendall("noplayers".encode())

def get_lobby_options(lobby_name, game_mode, region, cm_pick):
    lobbyGameMode = DOTA_GameMode.DOTA_GAMEMODE_AP
    if(game_mode == "AllPick"):
        lobbyGameMode = DOTA_GameMode.DOTA_GAMEMODE_AP
    elif(game_mode == "CaptainsMode"):
        lobbyGameMode = DOTA_GameMode.DOTA_GAMEMODE_CM
    elif(game_mode == "OneVOne"):
        lobbyGameMode = DOTA_GameMode.DOTA_GAMEMODE_1V1MID

    serverRegion = EServerRegion.USWest
    if(region == "na"):
        serverRegion = EServerRegion.USWest
    if(region == "sa"):
        serverRegion = EServerRegion.Brazil
    if(region == "eu"):
        serverRegion = EServerRegion.Europe
    if(region == "cis"):
        serverRegion = EServerRegion.Stockholm
    if(region == "sea"):
        serverRegion = EServerRegion.Japan

    lobby_options = {
        "game_mode": lobbyGameMode,  # CAPTAINS MODE
        "allow_cheats": False,
        "fill_with_bots": False,
        "intro_mode": False,
        "game_name": lobby_name,
        "server_region": serverRegion,
        "cm_pick": cm_pick,
        "allow_spectating": True,
        "bot_difficulty_radiant": 4,  # BOT_DIFFICULTY_UNFAIR
        "game_version": 0,  # GAME_VERSION_CURRENT
        "pass_key": "",
        "leagueid": 0,
        "penalty_level_radiant": 0,
        "penalty_level_dire": 0,
        "series_type": 0,
        "radiant_series_wins": 0,
        "dire_series_wins": 0,
        "allchat": False,
        "dota_tv_delay": 1,  # LobbyDotaTV_120
        "lan": False,
        "visibility": 0,  # DOTALobbyVisibility_Public
        "previous_match_override": 0,
        "pause_setting": 0,  # LobbyDotaPauseSetting_Unlimited
        "bot_difficulty_dire": 4,  # BOT_DIFFICULTY_UNFAIR
        "bot_radiant": 0,
        "bot_dire": 0,
        "selection_priority_rules": 0,
        "league_node_id": 0,
    }

    return lobby_options

def create_lobby(lobby_name, lobby_password, game_mode, region, cm_pick):
    lobby_options = get_lobby_options(lobby_name, game_mode, region, cm_pick)

    dota.create_practice_lobby(password=lobby_password, options=lobby_options)


def destroy_lobby():
    if dota.lobby is not None:
        print("Destroyed Lobby", flush=True)
        dota.destroy_lobby()


def send_lobby_invite(player_ids):
    for player_id in player_ids:
        dota.invite_to_lobby(player_id)


@client.on('logged_on')
def start_dota():
    print("start_dota", flush=True)
    dota.launch()


@dota.on('ready')
def do_dota_stuff():
    print("do_dota_stuff", flush=True)
    isReady = True
    readyMsg = "ready"
    sock.sendall(readyMsg.encode())
    destroy_lobby()


@dota.on('lobby_invite')
def invited(invite):
    lobby_id = invite.group_id
    dota.respond_to_lobby_invite(lobby_id=lobby_id, accept=False)


@dota.on('lobby_new')
def on_lobby_join(lobby):
    # Leave player slot
    print(f"Joined lobby: {lobby.lobby_id}", flush=True)
    dota.join_practice_lobby_team(slot=1)
    dota.channels.join_lobby_channel()

@dota.on('lobby_changed')
def lobby_change(lobby):
    update_slot(lobby)
    check_to_start(lobby)
    global draftStarted
    global matchEnded
    if(lobby.state == 2 and not draftStarted):
        draftStarted = True
        sock.sendall("draftstarted".encode())
    elif(lobby.state == 3 and not matchEnded):
        matchEnded = True
        sock.sendall("matchended".encode())


@dota.channels.on(dota2.features.chat.ChannelManager.EVENT_JOINED_CHANNEL)
def on_join_channel(channel):
    print(f"Joined chat channel: {channel}", flush=True)

@dota.channels.on(dota2.features.chat.ChannelManager.EVENT_MESSAGE)
def on_message(channel, msg):
    if channel.type == DOTAChatChannelType_t.DOTAChannelType_Lobby:
        print(f"{msg.persona_name} says: \"{msg.text}\"", flush=True)
        if msg.text[0] == ";":
            splitMessage = shlex.split(msg.text)
            if splitMessage[0] == ";start":
                channel.send("Starting match...")
                start_lobby()
            elif splitMessage[0] == ";coinflip":
                channel.send("Performing coin flip...")
                coin_flip()


def run_client_thread():
    client.run_forever()
        

sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
sock.connect(("127.0.0.1", 42069))

loginResult = client.login(username=config.username, password=config.password)
if loginResult != EResult.OK:
    print(f"Login failed with error: {loginResult}", flush=True)
else:
    print("Logged in successfully.", flush=True)

clientThread = threading.Thread(target=run_client_thread)
clientThread.start()

while True:
    data = sock.recv(1024)
    if not data:
        continue
    message = data.decode()
    splitMessage = shlex.split(message)
    if len(splitMessage) == 0:
        continue
    
    if splitMessage[0] == "createlobby":
        lobbyName = splitMessage[1]
        create_lobby(lobbyName, splitMessage[2], splitMessage[3], splitMessage[4], int(splitMessage[5]))
    if splitMessage[0] == "reset":
        draftStarted = False
        matchEnded = False
        destroy_lobby()
    if(splitMessage[0] == "startmatch"):
        start_lobby()
