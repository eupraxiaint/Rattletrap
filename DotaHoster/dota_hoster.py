# from https://github.com/ziadoma/dota_hoster

from steam.client import SteamClient
from dota2.client import Dota2Client
import config

client = SteamClient()
dota = Dota2Client(client)

admins = config.admins
lobby_slots = [""] * 10


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
    print(lobby_slots)


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


def start_lobby():
    dota.launch_practice_lobby()


def create_lobby(lobby_name, lobby_password):
    lobby_options = {
        "game_mode": 2,  # CAPTAINS MODE
        "allow_cheats": False,
        "fill_with_bots": False,
        "intro_mode": False,
        "game_name": lobby_name,
        "server_region": 3,
        "cm_pick": 0,
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
        "selection_priority_rules": 1,  # k_DOTASelectionPriorityRules_Automatic
        "league_node_id": 0,
    }

    dota.create_tournament_lobby(password=lobby_password, tournament_game_id=None, tournament_id=0,
                                 options=lobby_options)


def destroy_lobby():
    if dota.lobby is not None:
        print("Destroyed Lobby")
        dota.destroy_lobby()


def send_lobby_invite(player_ids):
    for player_id in player_ids:
        dota.invite_to_lobby(player_id)


@client.on('logged_on')
def start_dota():
    dota.launch()


@dota.on('ready')
def do_dota_stuff():
    destroy_lobby()
    create_lobby("ducks", "ducks")


@dota.on('lobby_invite')
def invited(invite):
    lobby_id = invite.group_id
    dota.respond_to_lobby_invite(lobby_id=lobby_id, accept=False)


@dota.on('lobby_new')
def on_lobby_join(lobby):
    # Leave player slot
    print("Joined lobby: {lobby.lobby_id}")
    dota.join_practice_lobby_team(slot=1)


@dota.on('lobby_changed')
def lobby_change(lobby):
    update_slot(lobby)
    check_to_start(lobby)


@dota.on('message')
def on_message(message):
    print(message)


client.cli_login(username=config.username, password=config.password)
client.run_forever()