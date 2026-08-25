-- Nortrans Reference database — schema
-- Consumed by Module 1 (consignee master data) and Module 5 (yard, containers, movements).
DROP TABLE IF EXISTS movements, containers, consignees, branches CASCADE;

CREATE TABLE branches (
    id    INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code  TEXT NOT NULL UNIQUE,
    name  TEXT NOT NULL,
    city  TEXT NOT NULL,
    phone TEXT NOT NULL
);

-- Deliberately all TEXT and without constraints: this table is the spreadsheet export that
-- Module 1 has to validate, defects included. Do not clean it.
CREATE TABLE consignees (
    id                 INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    legal_name         TEXT NOT NULL DEFAULT '',
    trading_name       TEXT NOT NULL DEFAULT '',
    tax_id             TEXT NOT NULL DEFAULT '',
    country_code       TEXT NOT NULL DEFAULT '',
    city               TEXT NOT NULL DEFAULT '',
    address_line       TEXT NOT NULL DEFAULT '',
    email              TEXT NOT NULL DEFAULT '',
    phone              TEXT NOT NULL DEFAULT '',
    incoterm           TEXT NOT NULL DEFAULT '',
    credit_limit_cents TEXT NOT NULL DEFAULT '0',
    active             TEXT NOT NULL DEFAULT 'true'
);

CREATE TABLE containers (
    id                   INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    container_no         TEXT NOT NULL UNIQUE,
    size_type            TEXT NOT NULL,
    teu                  NUMERIC(3,1) NOT NULL,
    seal_no              TEXT NOT NULL,
    bill_of_lading       TEXT NOT NULL,
    consignee_legal_name TEXT NOT NULL,
    current_branch_code  TEXT NOT NULL,
    status               TEXT NOT NULL,
    last_movement_at     TIMESTAMPTZ
);

CREATE TABLE movements (
    id            INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    container_no  TEXT        NOT NULL REFERENCES containers(container_no),
    branch_code   TEXT        NOT NULL REFERENCES branches(code),
    movement_type TEXT        NOT NULL CHECK (movement_type IN
                      ('Gate In','Gate Out','Stripped','Stuffed','Damaged','Reweighed')),
    occurred_at   TIMESTAMPTZ NOT NULL,
    note          TEXT        NOT NULL DEFAULT ''
);

CREATE INDEX ix_movements_container ON movements(container_no);
CREATE INDEX ix_movements_branch    ON movements(branch_code);
CREATE INDEX ix_containers_branch   ON containers(current_branch_code);
