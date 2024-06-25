# OTUS Postgres indexes demo

Данный репозиторий содержит генератор тестовой базы данных для открытого урока по Postgres.

## Запуск

Для запуска и генерации тестовых данных нужно запустить

```
docker-compose up -d
```

После того, как база данных будет создана и тестовые даннные будут заполнены достаточно запускать только Postgres командой

```
docker-compose up -d postgres
```

```
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
```

## EXPLAIN ANALYZE

- Seq Scan - последовательный перебор строк таблицы
- Index Only Scan - поиск по индексу без чтения в основной таблице
- Index Scan - поиск по индексу с чтением из основной таблицы дополнительных столбцов
- Bitmap Heap Scan - собирает указатели на строки в таблице из индекса и сортирует их по местоположению на диске
- Nested Loops - соединение вложенными циклами
- Hash - вычисление хеш-таблицы для отобранных строк
- Hash Join - соединение записей с помощью хеш-таблицы
- Merge Join - соединение заранее отсортированных  наборов данных с помощью алгоритмов слияния
- Sort - Сортировка записей
- Limit - ограничение отбираемых строк для дальнейшей обработки
- Gather - объединение результатов параллельного выполнения узлов



## Первичные ключи

Несмотря на то, что по первичному ключу всегда строится индекс, планировщик может не всегда его использовать.

```sql
explain analyze
select id, first_name, last_name, email
from public.users u
where id > 185
limit 10;
```

```
Limit  (cost=0.00..1.00 rows=10 width=40) (actual time=0.010..0.013 rows=10 loops=1)
  ->  Seq Scan on users u  (cost=0.00..99567.84 rows=999944 width=40) (actual time=0.008..0.010 rows=10 loops=1)
        Filter: (id > 185)
Planning Time: 0.108 ms
Execution Time: 0.026 ms
```

Использование индекса может зависеть от селективности данных.

```sql
explain analyze
select id, first_name, last_name, email
from public.users u
where id = 185;
```

```
Index Scan using pk_users on users u  (cost=0.42..8.44 rows=1 width=40) (actual time=0.020..0.020 rows=1 loops=1)
  Index Cond: (id = 185)
Planning Time: 0.051 ms
Execution Time: 0.033 ms
```

## Индекс BTREE

https://www.cs.usfca.edu/~galles/visualization/BPlusTree.html

Используется:
- операторы сравнения >, <, =, >=, <=, BETWEEN и IN;
- условия пустоты IS NULL и IS NOT NULL;
- операторы поиска подстроки LIKE и ~, если искомая строка закреплена в начале шаблона (например name LIKE 'Lisa%');
- регистронезависимые операторы поиска подстроки ILIKE и ~*. Но только в том случае, если искомая строка начинается с символа, который одинаков и в верхнем и в нижнем регистре (например числа)`.

### Полному совпадению текста

```sql
create index if not exists idx_email_btree on users using btree(email);
```

```sql
explain analyze
SELECT id, first_name, last_name, email
FROM public.users
where email = 'Nasir14@gmail.com'
```
```
Index Scan using idx_email_btree on users  (cost=0.42..8.44 rows=1 width=40) (actual time=0.533..0.535 rows=1 loops=1)
  Index Cond: (email = 'Nasir14@gmail.com'::text)
Planning Time: 0.769 ms
Execution Time: 0.549 ms
```

### Поиск по префиксу лучше делать с расширением text_pattern_ops

```sql
explain analyze
SELECT id, first_name, last_name, email
FROM public.users
where email like 'Teagan.Homenick%'
```

```
Gather  (cost=1000.00..93283.23 rows=100 width=40) (actual time=4419.957..4421.806 rows=1 loops=1)
  Workers Planned: 2
  Workers Launched: 2
  ->  Parallel Seq Scan on users  (cost=0.00..92273.23 rows=42 width=40) (actual time=4357.138..4357.140 rows=0 loops=3)
        Filter: (email ~~ 'Teagan.Homenick%'::text)
        Rows Removed by Filter: 333333
Planning Time: 2.929 ms
Execution Time: 4421.824 ms
```

```sql
drop index idx_email_btree;
create index if not exists idx_email_btree on users using btree(email text_pattern_ops);
```

```sql
explain analyze
SELECT id, first_name, last_name, email
FROM public.users
where email like 'Teagan.Homenick%'
```

```
Index Scan using idx_email_btree on users  (cost=0.42..8.45 rows=100 width=40) (actual time=0.471..0.472 rows=1 loops=1)
  Index Cond: ((email ~>=~ 'Teagan.Homenick'::text) AND (email ~<~ 'Teagan.Homenicl'::text))
  Filter: (email ~~ 'Teagan.Homenick%'::text)
Planning Time: 3.061 ms
Execution Time: 0.490 ms
```

### Поиск по постфиксу через индекс не пойдет

```sql
explain analyze
SELECT id, first_name, last_name, email
FROM public.users
where email like '%gmail.com'
```

```
Seq Scan on users  (cost=0.00..99565.20 rows=313111 width=40) (actual time=0.014..25276.231 rows=333040 loops=1)
  Filter: (email ~~ '%gmail.com'::text)
  Rows Removed by Filter: 666960
Planning Time: 0.106 ms
Execution Time: 25315.553 ms
```



### Агрегирующие функции

```sql
create index if not exists idx_score_btree on users using btree(score);
```

```sql
explain analyze
select min(score) FROM public.users
```

```
Result  (cost=0.45..0.46 rows=1 width=4) (actual time=0.014..0.015 rows=1 loops=1)
  InitPlan 1 (returns $0)
    ->  Limit  (cost=0.42..0.45 rows=1 width=4) (actual time=0.011..0.012 rows=1 loops=1)
          ->  Index Only Scan using idx_score_btree on users  (cost=0.42..20918.29 rows=999878 width=4) (actual time=0.010..0.011 rows=1 loops=1)
                Index Cond: (score IS NOT NULL)
                Heap Fetches: 0
Planning Time: 0.110 ms
Execution Time: 0.029 ms
```



```sql
explain analyze
select avg(score) FROM public.users
```

```
Finalize Aggregate  (cost=14627.73..14627.74 rows=1 width=32) (actual time=225.694..228.810 rows=1 loops=1)
  ->  Gather  (cost=14627.51..14627.72 rows=2 width=32) (actual time=225.628..228.799 rows=3 loops=1)
        Workers Planned: 2
        Workers Launched: 2
        ->  Partial Aggregate  (cost=13627.51..13627.52 rows=1 width=32) (actual time=148.828..148.829 rows=1 loops=3)
              ->  Parallel Index Only Scan using idx_score_btree on users  (cost=0.42..12585.97 rows=416616 width=4) (actual time=1.054..124.692 rows=333333 loops=3)
                    Heap Fetches: 0
Planning Time: 1.174 ms
Execution Time: 228.846 ms
```

### Диапазон значений

```sql
explain analyze
SELECT id, first_name, last_name, email, birthdate
FROM public.users
where score > 99
```

```
Bitmap Heap Scan on users  (cost=113.82..28686.10 rows=9986 width=48) (actual time=7.308..5614.373 rows=9971 loops=1)
  Recheck Cond: (score > 99)
  Heap Blocks: exact=9483
  ->  Bitmap Index Scan on idx_score_btree  (cost=0.00..111.32 rows=9986 width=0) (actual time=3.119..3.120 rows=9971 loops=1)
        Index Cond: (score > 99)
Planning Time: 0.214 ms
Execution Time: 5617.812 ms
```

## Индекс HASH

Используется при условии равенства.

```sql
create index if not exists idx_email_hash on users using hash(email);
```

```sql
explain analyze
SELECT id, first_name, last_name, email
FROM public.users
where email = 'Teagan.Homenick@yahoo.com'
```

```
Index Scan using idx_email_hash on users  (cost=0.00..8.02 rows=1 width=40) (actual time=0.028..0.029 rows=1 loops=1)
  Index Cond: (email = 'Teagan.Homenick@yahoo.com'::text)
Planning Time: 0.072 ms
Execution Time: 0.046 ms
```

```sql
explain analyze
SELECT id, first_name, last_name, email
FROM public.users
where email like 'Teagan.Homenick%'
```

```
Gather  (cost=1000.00..93285.03 rows=100 width=40) (actual time=4191.596..4193.711 rows=1 loops=1)
  Workers Planned: 2
  Workers Launched: 2
  ->  Parallel Seq Scan on users  (cost=0.00..92275.03 rows=42 width=40) (actual time=4127.258..4127.260 rows=0 loops=3)
        Filter: (email ~~ 'Teagan.Homenick%'::text)
        Rows Removed by Filter: 333333
Planning Time: 0.098 ms
Execution Time: 4193.725 ms
```

Индекс HASH по сравнению с BTREE занимает меньше места

|index_name|index_size|
|----------|----------|
|idx_email_btree|42 MB|
|idx_email_hash|32 MB|


## Индекс GIN

### Массивы

```sql
CREATE INDEX idx_tags_gin ON users USING GIN (tags);
```

```sql
explain analyze
SELECT id, first_name, last_name, email
FROM public.users
WHERE 'Computers' = ANY(tags);
```

```
Gather  (cost=1000.00..101950.18 rows=45097 width=40) (actual time=4.579..3715.927 rows=44840 loops=1)
  Workers Planned: 2
  Workers Launched: 2
  ->  Parallel Seq Scan on users  (cost=0.00..96440.48 rows=18790 width=40) (actual time=4.609..3627.416 rows=14947 loops=3)
        Filter: ('Computers'::text = ANY (tags))
        Rows Removed by Filter: 318387
Planning Time: 4.632 ms
JIT:
  Functions: 12
  Options: Inlining false, Optimization false, Expressions true, Deforming true
  Timing: Generation 0.694 ms, Inlining 0.000 ms, Optimization 0.737 ms, Emission 12.276 ms, Total 13.708 ms
Execution Time: 3719.401 ms
```

```sql
explain analyze
SELECT id, first_name, last_name, email
FROM public.users
WHERE tags @> ARRAY['Kids & Kids', 'Health'];
```

```
Bitmap Heap Scan on users  (cost=30.52..406.16 rows=96 width=40) (actual time=0.546..0.619 rows=67 loops=1)
  Recheck Cond: (tags @> '{"Kids & Kids",Health}'::text[])
  Heap Blocks: exact=67
  ->  Bitmap Index Scan on idx_tags_gin  (cost=0.00..30.50 rows=96 width=0) (actual time=0.533..0.533 rows=67 loops=1)
        Index Cond: (tags @> '{"Kids & Kids",Health}'::text[])
Planning Time: 0.136 ms
Execution Time: 0.637 ms
```

### JSONB

```sql
create index if not exists idx_jsonb_gin on users using gin(user_json_data);
```

```sql
explain analyze
SELECT id, first_name, last_name, email
FROM public.users
where user_json_data @> '{"Score": 83}';
```

```
Bitmap Heap Scan on users  (cost=26.14..417.23 rows=100 width=40) (actual time=3.271..2072.703 rows=9985 loops=1)
  Recheck Cond: (user_json_data @> '{"Score": 83}'::jsonb)
  Heap Blocks: exact=9450
  ->  Bitmap Index Scan on idx_jsonb_gin  (cost=0.00..26.12 rows=100 width=0) (actual time=1.768..1.769 rows=9985 loops=1)
        Index Cond: (user_json_data @> '{"Score": 83}'::jsonb)
Planning Time: 0.222 ms
Execution Time: 2075.258 ms
```

```sql
explain analyze
SELECT id, first_name, last_name, email
FROM public.users
WHERE user_json_data -> 'tags' @> '["Kids & Kids", "Health"]'::JSONB;
```

## Индекс GIST

```sql
create index if not exists idx_ip_address_btree on users using btree(ip_address);

explain analyze
SELECT id, first_name, last_name, email , ip_address
FROM users
WHERE ip_address like '90.79.21.%';
```

```
Gather  (cost=1000.00..105487.94 rows=100 width=54) (actual time=4420.194..4425.117 rows=1 loops=1)
  Workers Planned: 2
  Workers Launched: 2
  ->  Parallel Seq Scan on users  (cost=0.00..104477.94 rows=42 width=54) (actual time=4363.563..4363.565 rows=0 loops=3)
        Filter: (ip_address ~~ '90.79.21.%'::text)
        Rows Removed by Filter: 333333
Planning Time: 0.102 ms
JIT:
  Functions: 12
  Options: Inlining false, Optimization false, Expressions true, Deforming true
  Timing: Generation 0.922 ms, Inlining 0.000 ms, Optimization 0.771 ms, Emission 11.053 ms, Total 12.746 ms
Execution Time: 4425.553 ms
```

```sql
create index if not exists idx_ip_address_gist on users  using gist (ip_address_inet inet_ops);

explain analyze
SELECT id, first_name, last_name, email, ip_address_inet
FROM users
WHERE ip_address_inet << '90.79.21.0/24';
```

```
Index Scan using idx_ip_address_gist on users  (cost=0.29..8.30 rows=1 width=47) (actual time=0.036..0.037 rows=1 loops=1)
  Index Cond: (ip_address_inet << '90.79.21.0/24'::inet)
Planning Time: 0.056 ms
Execution Time: 0.049 ms
```


## Полезные запросы

### Определение размеров индексов

```sql
SELECT
  *,
  pg_size_pretty(table_bytes) AS table,
  pg_size_pretty(index_bytes) AS index,
  pg_size_pretty(total_bytes) AS total
FROM (
  SELECT
    *, total_bytes - index_bytes - COALESCE(toast_bytes, 0) AS table_bytes
  FROM (
    SELECT
      c.oid,
      nspname AS table_schema,
      relname AS table_name,
      c.reltuples AS row_estimate,
      pg_total_relation_size(c.oid) AS total_bytes,
      pg_indexes_size(c.oid) AS index_bytes,
      pg_total_relation_size(reltoastrelid) AS toast_bytes
    FROM
      pg_class c
      LEFT JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE relkind = 'r'
  ) a
) a
WHERE table_schema = 'public'
ORDER BY total_bytes DESC;
```

```sql
SELECT
    t.relname AS table_name,
    pg_size_pretty(pg_total_relation_size(t.oid)) AS total_table_size,
    idx.relname AS index_name,
    pg_size_pretty(pg_relation_size(idx.oid)) AS index_size
FROM
    pg_class t
JOIN
    pg_index i ON t.oid = i.indrelid
JOIN
    pg_class idx ON idx.oid = i.indexrelid
WHERE
    t.relkind = 'r' -- 'r' denotes a table, exclude other objects like views
    AND t.relname = 'users' -- Replace with your table name
ORDER BY
    pg_total_relation_size(t.oid) DESC, pg_relation_size(idx.oid) DESC;
```

### Поиск медленных запросов

```sql
SELECT * FROM pg_stat_statements ORDER BY total_time DESC;
```

### Поиск отсутствующих индексов

Запрос находит таблицы, в которых было больше последовательных сканирований Seq Scan, чем индексных сканирований Index Scan — явный признак того, что индекс поможет.
Это не скажет вам, по каким столбцам создать индекс, так что потребуется немного больше работы.
Однако, знание, какие таблицы нуждаются в них, это хороший первый шаг.

```sql
SELECT
  relname,
  seq_scan - idx_scan AS too_much_seq,
  CASE
    WHEN
      seq_scan - coalesce(idx_scan, 0) > 0
    THEN
      'Missing Index?'
    ELSE
      'OK'
  END,
  pg_relation_size(relname::regclass) AS rel_size, seq_scan, idx_scan
FROM
  pg_stat_all_tables
WHERE
  schemaname = 'public'
  AND pg_relation_size(relname::regclass) > 80000
ORDER BY
  too_much_seq DESC;
```

### Поиск ненужных индексов

```sql
SELECT schemaname, relname, indexrelname
FROM pg_stat_all_indexes
WHERE idx_scan = 0 and schemaname <> 'pg_toast' and  schemaname <> 'pg_catalog'
```

```sql
SELECT
  indexrelid::regclass as index,
  relid::regclass as table,
  'DROP INDEX ' || indexrelid::regclass || ';' as drop_statement
FROM
  pg_stat_user_indexes
  JOIN
    pg_index USING (indexrelid)
WHERE
  idx_scan = 0
  AND indisunique is false;
```

## Приемы оптимизации

### Составные индексы

```sql
create index if not exists idx_first_btree on users using btree(first_name text_pattern_ops);
create index if not exists idx_last_btree on users using btree(last_name text_pattern_ops);

explain analyze
SELECT id, first_name, last_name, email, birthdate
FROM public.users
where first_name like 'An%' and last_name like 'Tr%'
```

```
Bitmap Heap Scan on users  (cost=407.31..1160.49 rows=202 width=48) (actual time=7.019..54.416 rows=189 loops=1)
  Filter: ((first_name ~~ 'An%'::text) AND (last_name ~~ 'Tr%'::text))
  Heap Blocks: exact=189
  ->  BitmapAnd  (cost=407.31..407.31 rows=194 width=0) (actual time=6.780..6.781 rows=0 loops=1)
        ->  Bitmap Index Scan on idx_last_btree  (cost=0.00..132.85 rows=9642 width=0) (actual time=2.160..2.161 rows=8618 loops=1)
              Index Cond: ((last_name ~>=~ 'Tr'::text) AND (last_name ~<~ 'Ts'::text))
        ->  Bitmap Index Scan on idx_first_btree  (cost=0.00..274.12 rows=20169 width=0) (actual time=4.215..4.215 rows=18214 loops=1)
              Index Cond: ((first_name ~>=~ 'An'::text) AND (first_name ~<~ 'Ao'::text))
Planning Time: 5.800 ms
Execution Time: 54.484 ms
```

```sql
create index if not exists idx_fullname_btree on users using btree(first_name text_pattern_ops, last_name text_pattern_ops);
```

```sql
explain analyze
SELECT id, first_name, last_name, email, birthdate
FROM public.users
where first_name like 'An%' and last_name like 'Tr%'
```

```
Index Scan using idx_last_btree on users  (cost=0.42..8.45 rows=2 width=48) (actual time=5.610..729.282 rows=189 loops=1)
  Index Cond: ((last_name ~>=~ 'Tr'::text) AND (last_name ~<~ 'Ts'::text))
  Filter: ((first_name ~~ 'An%'::text) AND (last_name ~~ 'Tr%'::text))
  Rows Removed by Filter: 8429
Planning Time: 4.515 ms
Execution Time: 729.355 ms
```

```sql
create index if not exists idx_score_rank_btree on users(score, rank);

explain analyze
SELECT first_name, last_name, email
FROM public.users
where score = 99 or rank > 99
```

```
Bitmap Heap Scan on users  (cost=27136.70..76100.29 rows=19504 width=36) (actual time=576.096..6055.437 rows=19891 loops=1)
  Recheck Cond: ((score = 99) OR (rank > '99'::numeric))
  Heap Blocks: exact=18178
  ->  BitmapOr  (cost=27136.70..27136.70 rows=19600 width=0) (actual time=573.620..573.621 rows=0 loops=1)
        ->  Bitmap Index Scan on idx_score_rank_btree  (cost=0.00..266.58 rows=9887 width=0) (actual time=1.118..1.118 rows=9933 loops=1)
              Index Cond: (score = 99)
        ->  Bitmap Index Scan on idx_score_rank_btree  (cost=0.00..26860.37 rows=9713 width=0) (actual time=572.500..572.501 rows=10047 loops=1)
              Index Cond: (rank > '99'::numeric)
Planning Time: 5.471 ms
Execution Time: 6058.400 ms
```

### Покрывающие индексы

```sql
create index if not exists idx_email_btree on users(email);

explain analyze
SELECT first_name, last_name, email
FROM public.users
where email = 'Jewel5@hotmail.com'
```

```
Index Scan using idx_email_btree on users  (cost=0.42..8.44 rows=1 width=36) (actual time=0.414..0.653 rows=3 loops=1)
  Index Cond: (email = 'Jewel5@hotmail.com'::text)
Planning Time: 3.019 ms
Execution Time: 0.665 ms
```

```sql
create index if not exists idx_email_name_btree on users(email, first_name, last_name);

explain analyze
SELECT first_name, last_name, email
FROM public.users
where email = 'Jewel5@hotmail.com'
```

```
Index Only Scan using idx_email_name_btree on users  (cost=0.42..4.44 rows=1 width=36) (actual time=1.313..1.317 rows=3 loops=1)
  Index Cond: (email = 'Jewel5@hotmail.com'::text)
  Heap Fetches: 0
Planning Time: 2.205 ms
Execution Time: 1.335 ms
```

## Список материалов для изучения

- PostgreSQL Query Profiler: как сопоставить план и запрос
  https://habr.com/ru/company/tensor/blog/517652/
- Индексы в PostgreSQL
  https://habr.com/ru/companies/postgrespro/articles/326096/
- PostgreSQL: практические примеры оптимизации SQL-запросов
  https://youtu.be/dm_oid1HVfQ
- Учебный курс по оптимизации запросов
  https://postgrespro.ru/education/courses/QPT
- PostgreSQL 16 изнутри
  https://postgrespro.ru/education/books/internals