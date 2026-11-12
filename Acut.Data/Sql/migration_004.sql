-- Захисна стійка має зберігатися між ходами та після завантаження.
ALTER TABLE ActiveBattle ADD COLUMN PlayerDefending INTEGER NOT NULL DEFAULT 0 CHECK (PlayerDefending IN (0, 1));
ALTER TABLE ActiveBattle ADD COLUMN EnemyDefending INTEGER NOT NULL DEFAULT 0 CHECK (EnemyDefending IN (0, 1));
