# MapChooser
This plugin allows you to change map, nominate map, rtv

# Installation
1. Install [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp) and [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)

2. Download MapChooser

3. Unzip the archive and upload it to the game server

# Configs
The config is created automatically in the same place where the dll is located
```
{
  "RtvTime": 10, //How long before a card change is available (in seconds)
  "Needed": 0.6, //Percentage of players needed to rockthevote
  "TimeVote": 20, //How long before the end of the map to start voting
  "PlayedMaps": 5, //How many maps must be played to unlock a card that has already been played
  "StartVoteChangeMap": 2.0, //How long to start voting on a completed map (in minutes)
  "VotingRoundThreshold": 3, //How many rounds to extend the map for
  "ExtendTime": 10, //For how long to extend the card (in minutes)
  "ExtendRound": 10
}
```
Map customization.
The config looks like this (maps.json)
```
{
  "Category": [
	"Карта на прохождение",
	"Карты pvp"
  ],
  "Settings": {
    "Карта на прохождение": [
      {
        "workshop": [
          "mg_switch_course_v2",
          "mg_school_course_v3",
		  "mg_draw_course_v3",
          "mg_glave_course_v2",
          "mg_simpsons_course_v2",
		  "mg_lego_course_v2",
          "mg_sky_realm_v3",
		  "mg_sonic_course_v2"
        ],
        "map": [
        ]
      }
    ],
    "Карты pvp": [
      {
        "workshop": [
        ],
        "map": [
          "de_dust2",
          "de_inferno",
		      "cs_office",
		      "cs_italy"
        ]
      }
    ]
  },
  "Maps": [
    {
      "workshop": [
        "новая_карта_1",
        "новая_карта_2"
      ],
      "map": [
        "новая_карта_3",
        "новая_карта_4"
      ]
    }
  ]
}

```
If you don't have any categories, you just leave "Maps".
```{
  "Maps": [
    {
      "workshop": [
        "mg_switch_course_v2",
        "mg_school_course_v3"
      ],
      "map": [
        "de_dust2",
        "de_inferno"
      ]
    }
  ]
}
```
If you have maps by category just delete "Maps"
# Commands
**ccs_rtv**,**!rtv** - starts the map change process

**css_nominate**,**!nominate** - opens the map menu

**css_nextmap**,**!nextmap** - the next map

**css_timeleft**,**!timeleft** - how long until the end of the map

**css_revote**,**!revote** - allows you to reselect a card
