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

Leaderboard calculated from all projects down below
<h3>User</h3>
Relational DB. 
Splits into few tables:

<h4>users:</h4>
Have next fields:
-user_id(required, primary)
-user_name(required, varchar, unique)
-user_email(required, varchar)
-password_hash
-oauth_provider(unique)
-oauth_id(unique)
-created_at(timestamp)
-user_whoami(required, unique, used to connect into game, varchar, hash)
-user_photo(*_url)

<h4>game_linking:</h4>
*connected with user as 1:1(1 user - 1 connection)*
Fields:
-user_id(foreign, primary, number)
-connected_at(timestamp)
-removed_at(timestamp)
-connecting_hash(unique)
*user can connect only once to game, and if game deletes, user can't connect again to any other game version(narrative unit)
if used deletes game, he can never enter it at the same account 

<h4>user_achievements:</h4>
*connects user and achievements as N:N(many users - many achievements) *
Fields:
-user_achievement_id(primary)
-user_id(foreign, number)
-achievement_id(foreign, number)
-unlocked_at(timestamp)

<h4>achievement:</h4>
-achievement_id(primary)
-achievement_name(varchar)
-achievement_description(varchar)
-is_secret(bool)
*predefined list, could be easily updated(achievement get orchestrated by the game, on website only short description for achievements page, if achievement is secret, it hides name and description, until someone finds it)

<h4>donations</h4>
*connects user as 1:N(1 user - many donations)*
-donation_id
-user_id(foreign, null)
-donation_amount(numeric)
-commentary(varchar, nullable)
-donated_at(timestamp)
-status
-is_anonymous(bool)
-currency
-provider_payment_id(unique)
*Used for donators leaderboard



<h3>Stats:</h3>
MongoDB

<h4>stats_user:</h4>
-user_id(unique)
-user_whoami(copy)
-updated_at(timestamp)
-{enemies}:
    -enemies_killed
    -enemies_pursued
    -enemies_forgiven
-{friends}
    -friends_made
    -friends_killed
    -friends_freed
-{relations}:
    -Meowbert
    -Nyanbert
    -Emma
    -Clodth
    -Arp
    -Dgio
    -Llania
-{totals}
    -times_killed
    -times_won
    -time_played
-{progress}
    -challenges completed[]
    -achievements_got
    -combos_collected
    -rooms_visited
-{items}
    -items_collected
    -items_used
-{commands}
    -commands_collected
    -commands_used
-{pickups}:
    -pickups_collected
    -keys_used
    -bombs_used
    -wasted_money
    -raised_money
-{events}
    -secrets_found
    -events_triggered
    -chests_opened

<h4>runs</h4>
-user_id
-run_id(unique)
-{current_stats}
-started_at
-game_version
-ended_at
-successful


<h3>Newsletter:</h3>
<h4>users_copy</h4>
-user_id(primary)
-user_name(varchar)
*duplicated from project 1*

<h4>admins</h4>
-admin_id(primary)
-admin_login(varchar, unique)
-admin_password_hash(hash)
-admin_name(varchar)

<h4>newsletter:</h4>
*main block of newsletter, connects with admins 1:N(1 admin - many newsletters)*
-newsletter_id(primary)
-admin_id(foreign)
-published_at(timestamp)
-title
-main_info(text)
-image(_url)
-is_hidden(bool)

<h4>user_comments:</h4>
*connects with user 1:N(1 user - many comments)*
-comment_id(primary)
-user_id(foreign)
-comment(text)
-parent_comment_id(foreign to comment_id)
-newsletter_id(foreign)
-is_hidden(bool)


<h4>subscriptions</h4>
*if user subscribes, newsletter comes upon his email, (1:N 1 newsletter - many users)*
-subscription_id(primary)
-status(enum)
-consent_at(timestamp)
-unsubscribe_token
-user_id(foreign)
-user_email(unique)
-newsletter_id(foreign)








<h2>ER diagrams</h2>