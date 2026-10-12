BEGIN;
SET search_path TO newsletter, public;

INSERT INTO admins (admin_id, admin_login, admin_password_hash, admin_name)
VALUES (4001, 'editor_demo', 'demo-password-hash', 'Demo Editor')
ON CONFLICT (admin_id) DO NOTHING;

INSERT INTO users_copy (user_id, user_name, user_email, source_updated_at)
VALUES (1001, 'alex_player', 'alex@example.test', CURRENT_TIMESTAMP)
ON CONFLICT (user_id) DO NOTHING;

INSERT INTO newsletters
    (newsletter_id, admin_id, published_at, title, main_info, image_url, is_hidden)
VALUES
    (3001, 4001, CURRENT_TIMESTAMP, 'Development Update',
     'A short update from the game development team.', NULL, FALSE)
ON CONFLICT (newsletter_id) DO NOTHING;

INSERT INTO newsletter_delivery_settings
    (newsletter_id, delivery_mode, recipient_settings, reply_to_email)
VALUES
    (3001, 'Immediate', '{"subscribers": true}'::jsonb, 'team@example.test')
ON CONFLICT (newsletter_id) DO NOTHING;

INSERT INTO subscriptions
    (user_id, newsletter_id, status, user_email_snapshot)
VALUES
    (1001, 3001, 'Active', 'alex@example.test')
ON CONFLICT (user_id, newsletter_id) DO NOTHING;

INSERT INTO user_comments
    (comment_id, user_id, newsletter_id, comment_text)
VALUES
    (5001, 1001, 3001, 'Looking forward to the next update!')
ON CONFLICT (comment_id) DO NOTHING;

SELECT setval(pg_get_serial_sequence('newsletter.admins', 'admin_id'),
              GREATEST((SELECT COALESCE(MAX(admin_id), 1) FROM admins), 1));
SELECT setval(pg_get_serial_sequence('newsletter.newsletters', 'newsletter_id'),
              GREATEST((SELECT COALESCE(MAX(newsletter_id), 1) FROM newsletters), 1));
SELECT setval(pg_get_serial_sequence('newsletter.subscriptions', 'subscription_id'),
              GREATEST((SELECT COALESCE(MAX(subscription_id), 1) FROM subscriptions), 1));
SELECT setval(pg_get_serial_sequence('newsletter.user_comments', 'comment_id'),
              GREATEST((SELECT COALESCE(MAX(comment_id), 1) FROM user_comments), 1));

COMMIT;
