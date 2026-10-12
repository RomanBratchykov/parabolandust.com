const alexId = NumberLong("1001");
const startedAt = ISODate("2026-01-15T12:00:00Z");

db.stats_user.updateOne(
  { user_id: alexId },
  {
    $setOnInsert: {
      user_id: alexId,
      user_whoami: "game-link-alex-001", // Copy owned by Project 1.
      updated_at: startedAt,
      enemies: { enemies_killed: 12, enemies_pursued: 3, enemies_forgiven: 1 },
      friends: { friends_made: 2, friends_killed: 0, friends_freed: 1 },
      relations: { Meowbert: 4, Nyanbert: 2 },
      totals: { times_killed: 1, times_won: 2, time_played: NumberLong("5400") },
      progress: {
        challenges_completed: ["first_room"],
        achievements_got: ["First Steps"],
        combos_collected: 3,
        rooms_visited: 8
      },
      items: { items_collected: 14, items_used: 5 },
      pickups: { pickups_collected: 9, keys_used: 1, bombs_used: 2 }
    }
  },
  { upsert: true }
);

db.stats_user.updateOne(
  { user_id: NumberLong("1002") },
  {
    $setOnInsert: {
      user_id: NumberLong("1002"),
      user_whoami: "game-link-sam-002", // Copy owned by Project 1.
      updated_at: startedAt,
      totals: { times_killed: 4, times_won: 0, time_played: NumberLong("900") },
      progress: { challenges_completed: ["tutorial"] }
    }
  },
  { upsert: true }
);

db.runs.updateOne(
  { run_id: "run-demo-1001-001" },
  {
    $setOnInsert: {
      run_id: "run-demo-1001-001",
      user_id: alexId, // Logical reference to stats_user / Project 1.
      started_at: startedAt,
      ended_at: ISODate("2026-01-15T12:32:00Z"),
      game_version: "0.1.0-demo",
      successful: true,
      current_stats: { enemies_killed: 12, rooms_visited: 8, time_played: 1920 },
      player_snapshot: { user_name: "alex_player", user_whoami: "game-link-alex-001" }
    }
  },
  { upsert: true }
);

db.runs.updateOne(
  { run_id: "run-demo-1002-001" },
  {
    $setOnInsert: {
      run_id: "run-demo-1002-001",
      user_id: NumberLong("1002"), // Logical reference to stats_user / Project 1.
      started_at: startedAt,
      ended_at: null,
      game_version: "0.1.0-demo",
      current_stats: { enemies_killed: 4, rooms_visited: 2 }
    }
  },
  { upsert: true }
);

db.game_events.updateOne(
  { event_id: "event-demo-1001-001" },
  {
    $setOnInsert: {
      event_id: "event-demo-1001-001",
      event_type: "item_collected",
      user_id: alexId,
      run_id: "run-demo-1001-001", // Logical reference, no cross-collection FK.
      occurred_at: ISODate("2026-01-15T12:04:00Z"),
      payload: { item_id: "bronze_key", item_name: "Bronze Key", room: 2 }
    }
  },
  { upsert: true }
);

db.game_events.updateOne(
  { event_id: "event-demo-1002-001" },
  {
    $setOnInsert: {
      event_id: "event-demo-1002-001",
      event_type: "run_abandoned",
      user_id: NumberLong("1002"),
      run_id: "run-demo-1002-001",
      occurred_at: startedAt,
      payload: { room: 2, reason: "player_left" },
      client_context: { platform: "web", locale: "en" }
    }
  },
  { upsert: true }
);
db.game_events.updateOne(
  { event_id: "event-demo-1001-002" },
  {
    $setOnInsert: {
      event_id: "event-demo-1001-002",
      event_type: "secret_found",
      user_id: alexId,
      run_id: "run-demo-1001-001",
      occurred_at: ISODate("2026-01-15T12:12:00Z"),
      payload: { secret_id: "hidden_archive", discovered_with: "wall_switch" }
    }
  },
  { upsert: true }
);
