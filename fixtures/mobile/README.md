# Mobile JW Fixtures

Desensitized samples for the native mobile JW Auth + Schedule chain.

These files model real-response shapes without containing real student IDs, names, tokens, cookies, captcha data, or production snapshots.

- `sample_student_my_nested.json`: `student/my` profile shape with nested user info.
- `sample_curriculum_real_shape.json`: `student/curriculum` shape using object `data`, `kbList`, and `weekDates`.
- `sample_curriculum_missing_fields.json`: curriculum shape with partial course fields that should still normalize with warnings.
