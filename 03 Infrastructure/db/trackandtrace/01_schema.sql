-- Nortrans Track & Trace database — schema
-- Module 4 of the Skill 09 competition simulation.
DROP TABLE IF EXISTS events, containers, shipments, consignees, branches CASCADE;

CREATE TABLE branches (
    id      INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code    TEXT NOT NULL UNIQUE,
    name    TEXT NOT NULL,
    city    TEXT NOT NULL,
    phone   TEXT NOT NULL
);

CREATE TABLE consignees (
    id           INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    legal_name   TEXT        NOT NULL,
    tax_id       TEXT        NOT NULL UNIQUE,
    country_code TEXT        NOT NULL,
    email        TEXT        NOT NULL UNIQUE,
    active       BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE shipments (
    id              INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    reference       TEXT        NOT NULL UNIQUE,
    bill_of_lading  TEXT        NOT NULL,
    consignee_id    INT         NOT NULL REFERENCES consignees(id),
    branch_id       INT         NOT NULL REFERENCES branches(id),
    mode            TEXT        NOT NULL CHECK (mode IN ('ocean','air','land')),
    status          TEXT        NOT NULL CHECK (status IN ('booked','in_transit','arrived','cleared','closed')),
    etd             TIMESTAMPTZ NOT NULL,
    eta             TIMESTAMPTZ NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE containers (
    id            INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    shipment_id   INT           NOT NULL REFERENCES shipments(id),
    container_no  TEXT          NOT NULL UNIQUE,
    size_type     TEXT          NOT NULL,
    teu           NUMERIC(3,1)  NOT NULL,
    seal_no       TEXT          NOT NULL,
    discharged_at TIMESTAMPTZ,
    gated_out_at  TIMESTAMPTZ
);

CREATE TABLE events (
    id           INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    container_id INT         NOT NULL REFERENCES containers(id),
    event_type   TEXT        NOT NULL CHECK (event_type IN
                     ('discharged','gate_in','stripped','stuffed','gate_out','damaged')),
    occurred_at  TIMESTAMPTZ NOT NULL,
    location     TEXT        NOT NULL,
    note         TEXT
);

CREATE INDEX ix_shipments_consignee ON shipments(consignee_id);
CREATE INDEX ix_shipments_branch    ON shipments(branch_id);
CREATE INDEX ix_containers_shipment ON containers(shipment_id);
CREATE INDEX ix_events_container    ON events(container_id);
