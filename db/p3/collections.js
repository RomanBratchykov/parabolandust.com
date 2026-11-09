const collectionDefinitions = [
  {
    name: "stats_user",
    validator: {
      $jsonSchema: {
        bsonType: "object",
        required: ["user_id", "user_whoami", "updated_at", "totals"],
        properties: {
          user_id: { bsonType: "long" },
          user_whoami: { bsonType: "string" },
          updated_at: { bsonType: "date" },
          totals: {
            bsonType: "object",
            required: ["times_killed", "times_won", "time_played"],
            properties: {
              times_killed: { bsonType: "int", minimum: 0 },
              times_won: { bsonType: "int", minimum: 0 },
              time_played: { bsonType: "long", minimum: 0 }
            }
          },
          progress: { bsonType: "object" }
        }
      }
    }
  },
  {
    name: "runs",
    validator: {
      $jsonSchema: {
        bsonType: "object",
        required: ["run_id", "user_id", "started_at", "game_version", "current_stats"],
        properties: {
          run_id: { bsonType: "string" },
          user_id: { bsonType: "long" }, 
          started_at: { bsonType: "date" },
          ended_at: { bsonType: ["date", "null"] },
          game_version: { bsonType: "string" },
          current_stats: { bsonType: "object" },
          player_snapshot: { bsonType: "object" } 
        }
      }
    }
  },
  {
    name: "game_events",
    validator: {
      $jsonSchema: {
        bsonType: "object",
        required: ["event_id", "event_type", "user_id", "occurred_at", "payload"],
        properties: {
          event_id: { bsonType: "string" },
          event_type: { bsonType: "string" },
          user_id: { bsonType: "long" }, 
          run_id: { bsonType: ["string", "null"] },
          occurred_at: { bsonType: "date" },
          payload: { bsonType: "object" } 
        }
      }
    }
  }
];

for (const definition of collectionDefinitions) {
  if (db.getCollectionNames().includes(definition.name)) {
    db.runCommand({
      collMod: definition.name,
      validator: definition.validator,
      validationLevel: "strict",
      validationAction: "error"
    });
  } else {
    db.createCollection(definition.name, { validator: definition.validator });
  }
}

db.stats_user.createIndex({ user_id: 1 }, { unique: true, name: "ux_stats_user_id" });
db.runs.createIndex({ user_id: 1, started_at: -1 }, { name: "ix_runs_user_history" });
db.runs.createIndex({ run_id: 1 }, { unique: true, name: "ux_runs_run_id" });
db.game_events.createIndex({ event_id: 1 }, { unique: true, name: "ux_game_events_event_id" });
db.game_events.createIndex({ user_id: 1, occurred_at: -1 }, { name: "ix_events_user_time" });
