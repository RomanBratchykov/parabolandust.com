SET search_path TO accounts, public;

/*---procedures---*/
CREATE OR REPLACE PROCEDURE usp_user_create
(
    IN p_user_name VARCHAR(50),
    IN p_user_email VARCHAR(254),
    IN p_password_hash VARCHAR(255),
    IN p_oauth_provider VARCHAR(50),
    IN p_oauth_id VARCHAR(200),
    IN p_user_whoami VARCHAR(128),
    IN p_user_photo_url VARCHAR(2048),
    OUT p_user_id BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO accounts.users
        (user_name, user_email, password_hash, oauth_provider, oauth_id,
         user_whoami, user_photo_url)
    VALUES
        (p_user_name, p_user_email, p_password_hash, p_oauth_provider, p_oauth_id,
         p_user_whoami, p_user_photo_url)
    RETURNING user_id INTO p_user_id;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_user_get_by_id
(
    IN p_user_id BIGINT,
    INOUT p_user JSONB
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT jsonb_build_object(
        'user_id', user_id,
        'user_name', user_name,
        'user_email', user_email,
        'oauth_provider', oauth_provider,
        'created_at', created_at,
        'user_photo_url', user_photo_url,
        'row_version', row_version
    )
    INTO p_user
    FROM accounts.users
    WHERE user_id = p_user_id AND is_deleted IS FALSE;

    IF p_user IS NULL THEN
        RAISE EXCEPTION 'User % was not found.', p_user_id
            USING ERRCODE = 'P0002';
    END IF;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_user_update_profile
(
    IN p_user_id BIGINT,
    IN p_user_name VARCHAR(50),
    IN p_user_email VARCHAR(254),
    IN p_user_photo_url VARCHAR(2048),
    IN p_expected_row_version BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE accounts.users
    SET user_name = p_user_name,
        user_email = p_user_email,
        user_photo_url = p_user_photo_url,
        updated_at = CURRENT_TIMESTAMP,
        updated_by = CURRENT_USER,
        row_version = row_version + 1
    WHERE user_id = p_user_id
      AND is_deleted IS FALSE
      AND row_version = p_expected_row_version;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'User was not found or has changed; reload before updating.'
            USING ERRCODE = 'P0001';
    END IF;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_user_soft_delete
(
    IN p_user_id BIGINT,
    IN p_expected_row_version BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE accounts.users
    SET is_deleted = TRUE,
        updated_at = CURRENT_TIMESTAMP,
        updated_by = CURRENT_USER,
        row_version = row_version + 1
    WHERE user_id = p_user_id
      AND is_deleted IS FALSE
      AND row_version = p_expected_row_version;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'User was not found or has changed; reload before deleting.'
            USING ERRCODE = 'P0001';
    END IF;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_donation_create
(
    IN p_user_id BIGINT,
    IN p_donation_amount NUMERIC(12,2),
    IN p_currency CHAR(3),
    IN p_is_anonymous BOOLEAN,
    IN p_commentary VARCHAR(500),
    IN p_provider_payment_id VARCHAR(200),
    OUT p_donation_id BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_user_id IS NOT NULL AND NOT EXISTS
       (SELECT 1 FROM accounts.users
        WHERE user_id = p_user_id AND is_deleted IS FALSE) THEN
        RAISE EXCEPTION 'Active user % was not found.', p_user_id
            USING ERRCODE = 'P0002';
    END IF;

    INSERT INTO accounts.donations
        (user_id, donation_amount, currency, is_anonymous, commentary, provider_payment_id)
    VALUES
        (p_user_id, p_donation_amount, p_currency, p_is_anonymous,
         p_commentary, p_provider_payment_id)
    RETURNING donation_id INTO p_donation_id;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_donation_change_donation_status
(
    IN p_donation_id BIGINT,
    IN p_new_donation_status VARCHAR(20),
    IN p_expected_row_version BIGINT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_current_donation_status VARCHAR(20);
    v_current_row_version BIGINT;
BEGIN
    IF p_new_donation_status IS NULL OR p_new_donation_status NOT IN
       ('Pending', 'Completed', 'Failed', 'Refunded') THEN
        RAISE EXCEPTION 'Unknown donation status: %.', p_new_donation_status
            USING ERRCODE = 'P0001';
    END IF;

    SELECT donation_status, row_version, is_deleted
    INTO v_current_donation_status, v_current_row_version
    FROM accounts.donations
    WHERE donation_id = p_donation_id AND is_deleted IS FALSE
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Donation % was not found.', p_donation_id
            USING ERRCODE = 'P0002';
    END IF;

    IF p_expected_row_version IS NULL
       OR v_current_row_version <> p_expected_row_version THEN
        RAISE EXCEPTION 'Donation has changed; reload before updating.'
            USING ERRCODE = 'P0001';
    END IF;

    IF NOT
    (
        (v_current_donation_status = 'Pending' AND p_new_donation_status IN ('Completed', 'Failed'))
        OR (v_current_donation_status = 'Completed' AND p_new_donation_status = 'Refunded')
    ) THEN
        RAISE EXCEPTION 'Invalid donation status transition: % -> %.',
            v_current_donation_status, p_new_donation_status
            USING ERRCODE = 'P0001';
    END IF;

    UPDATE accounts.donations
    SET donation_status = p_new_donation_status,
        updated_at = CURRENT_TIMESTAMP,
        updated_by = CURRENT_USER,
        row_version = row_version + 1
    WHERE donation_id = p_donation_id
      AND is_deleted IS FALSE;
END;
$$;
