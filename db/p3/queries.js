print("Top players by wins:");
printjson(
  db.stats_user.aggregate([
    { $sort: { "totals.times_won": -1, "totals.times_killed": 1 } },
    {
      $project: {
        _id: 0,
        user_id: 1,
        user_whoami: 1,
        times_won: "$totals.times_won",
        times_killed: "$totals.times_killed"
      }
    },
    { $limit: 10 }
  ]).toArray()
);

print("Event counts for user 1001:");
printjson(
  db.game_events.aggregate([
    { $match: { user_id: NumberLong("1001") } },
    { $group: { _id: "$event_type", count: { $sum: 1 } } },
    { $sort: { count: -1, _id: 1 } }
  ]).toArray()
);
