-- Відновлюємо зв'язки каталогу з навичками; стан гравця та наявні будівлі не змінюємо.
UPDATE BuildingType
SET RequiredSkillId = CASE BuildingTypeId WHEN 2 THEN 1 WHEN 4 THEN 1 WHEN 5 THEN 2 WHEN 6 THEN 3 END
WHERE BuildingTypeId IN (2, 4, 5, 6)
  AND EXISTS (
      SELECT 1 FROM Skill
      WHERE SkillId = CASE BuildingTypeId WHEN 2 THEN 1 WHEN 4 THEN 1 WHEN 5 THEN 2 WHEN 6 THEN 3 END
  );
