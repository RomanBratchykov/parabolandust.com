<h1>Welcome to the</h1>


```text
    ____                   __          __            ____             __
   / __ \____ __________ _/ /_  ____  / /___ _____  / __ \__  _______/ /_
  / /_/ / __ `/ ___/ __ `/ __ \/ __ \/ / __ `/ __ \/ / / / / / / ___/ __/
 / ____/ /_/ / /  / /_/ / /_/ / /_/ / / /_/ / / / / /_/ / /_/ (__  ) /_
/_/    \__,_/_/   \__,_/_.___/\____/_/\__,_/_/ /_/_____/\__,_/____/\__/.com
```
Which is a website for our game

<h1>DB</h1>
<h2>Subject area</h2>
Site for videogame. Used by people, who wants to check what is this game, look over news, videos, photos, process of development.
Used for:
- Register/Login
- Newsletter
- Stats(Personal and leaderboard)
- Donations(Pre DEMO)
- Community network(Post DEMO)

Therefore it could be splitted into next instances
- User
- Statistics
- Newsletter

Flow of website:
User registers -> check newsletter -> check leaderboards -> check his own stats -> can modify his profile
<h3>User</h3>
Relational DB. 
Splits into few tables:

<h4>Table user:</h4>
Have next fields:
-user_id(required, primary)
-user_name(required, warchar)
-user_email(required, warchar)
-user_whoami(required, unique, used to connect into game, warchar)
-user_photo(not required, blob)

<h4>table is_connected</h4>
*connected with user as 1:1(1 user - 1 connection)*
Fields:
-user_id(foreign, number)
-is_connected(bool)
-connected_at(timestamp)
*user can connect only once to game, and if game deletes, user can't connect again to any other game version(narrative unit)

<h4>user_achievements</h4>
*connected with user as 1:N(1 user - many achievements) and with achievements*
Fields:
-user_id(foreign, number)
-achievement_id(foreign, number)
-get_at(timestamp)

<h4>achievement</h4>
*connected with user_achievements as N:1(many achievements - 1 user)*
-achievement_id(primary)
-achievement_name(warchar)
-achievement_description(warchar)
-is_secret(bool)
*predefined list, could be easily updated(achievement get orchestrated by the game, on website only short description for achievements page, if achievement is secret, it hides name and description, until someone finds it)


<h3>Stats</h3>
MongoDB




<h3>Newsletter</h3>







<h2>ER diagrams</h2>