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

<h4>user:</h4>
Have next fields:
-user_id(required, primary)
-user_name(required, varchar)
-user_email(required, varchar)
-user_password(hash, OAuth)
-user_whoami(required, unique, used to connect into game, varchar)
-user_photo(not required, blob)

<h4>is_connected:</h4>
*connected with user as 1:1(1 user - 1 connection)*
Fields:
-user_id(foreign, number)
-is_connected(bool)
-connected_at(timestamp)
*user can connect only once to game, and if game deletes, user can't connect again to any other game version(narrative unit)

<h4>user_achievements:</h4>
*connects user and achievements as N:N(many users - many achievements) *
Fields:
-user_id(foreign, number)
-achievement_id(foreign, number)
-unlocked_at(timestamp)

<h4>achievement:</h4>
-achievement_id(primary)
-achievement_name(varchar)
-achievement_description(varchar)
-is_secret(bool)
*predefined list, could be easily updated(achievement get orchestrated by the game, on website only short description for achievements page, if achievement is secret, it hides name and description, until someone finds it)
<h4>user_donations</h4>
*connects user as 1:N(1 user - many donations)*
-user_id(foreign)
-donation_amount(currency)
-commentary(varchar)
-donated_at(timestamp)
*Used for donators leaderboard



<h3>Stats:</h3>
MongoDB

<h4>stats_user:</h4>
-user_id
-enemies_killed
-enemies_pursued
-enemies_forgaven
-friends_made
-friends_killed
-friends_freed
-Meowbert_relation
-Nyanbert_relation
-Emma_relation
-Clodth_relation
-Arp_relation
-Dgio_relation
-Llania_relation
-times_killed
-times_won
-{challenges completed}
-secrets_found
-items_collected
-items_used
-commands_collected
-commands_used
-pickups_collected
-keys_used
-bombs_used
-achievements_got
-time_played
-events_triggered
-chests_opened
-wasted_money
-raised_money
-combos_collected
-111%_got

<h4>runs</h4>
-user_id
-run_id
-{current_stats}
-started_at
-ended_at
-succesful


<h3>Newsletter:</h3>

<h4>admins</h4>
-admin_id(primary)
-admin_login(varchar, unique)
-admin_password(hash)
-admin_name(varchar)
<h4>newsletter:</h4>
*main block of newsletter, connects with admins 1:N(1 admin - many newsletters)*
-newsletter_id(primary)
-admin_id(foreign)
-created_at(timestamp)
-main_info(text)
-image(blob)
-is_hidden(bool)

<h4>user_comments:</h4>
*connects with user 1:N(1 user - many comments)*
-comment_id(primary)
-user_id(foreign)
-comment(text)
-parent_comment(foreign to comment_id)
-is_hidden(bool)

<h4>comments_newsletter</h4>
*connection of newsletter and comments N:N(many comments - many newsletters)*
-comment_id(foreign)
-newsletter_id(foreign)


<h4>subscriptions</h4>
*if user subscribes, newsletter comes upon his email, (N:N many newsletters - many users)*
-user_id(foreign)
-newsletter_id(foreing)








<h2>ER diagrams</h2>