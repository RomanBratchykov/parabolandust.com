BEGIN;
SET search_path TO accounts, public;

INSERT INTO users
    (user_id, user_name, user_email, password_hash, user_whoami)
OVERRIDING SYSTEM VALUE
VALUES
    (1001, 'alex_player', 'alex@example.test', 'demo-password-hash',
     'game-link-alex-001')
ON CONFLICT (user_id) DO NOTHING;

INSERT INTO game_linking (user_id, connecting_hash)
VALUES (1001, 'connect-alex-demo-001')
ON CONFLICT (user_id) DO NOTHING;

INSERT INTO achievement
    (achievement_id, achievement_name, achievement_description, is_secret)
OVERRIDING SYSTEM VALUE
VALUES
    (2001, 'First Steps', 'Complete the opening section of the game.', FALSE),
    (2002, 'Secret Room', 'Find a hidden room.', TRUE)
ON CONFLICT (achievement_id) DO NOTHING;

INSERT INTO user_achievements (user_id, achievement_id)
VALUES (1001, 2001)
ON CONFLICT (user_id, achievement_id) DO NOTHING;

INSERT INTO donations
    (user_id, donation_amount, commentary, donation_status, is_anonymous, currency,
     provider_payment_id)
VALUES
    (1001, 15.00, 'Thanks for the demo!', 'Completed', FALSE, 'USD',
     'demo-payment-alex-001'),
    (NULL, 5.00, NULL, 'Pending', TRUE, 'USD', 'demo-payment-guest-001')
ON CONFLICT (provider_payment_id) WHERE provider_payment_id IS NOT NULL
DO NOTHING;

SELECT setval(pg_get_serial_sequence('accounts.users', 'user_id'),
              GREATEST((SELECT COALESCE(MAX(user_id), 1) FROM users), 1));
SELECT setval(pg_get_serial_sequence('accounts.achievement', 'achievement_id'),
              GREATEST((SELECT COALESCE(MAX(achievement_id), 1) FROM achievement), 1));

COMMIT;
