-- Draw the Odds: ranking global (mesmo projeto Supabase do Alien Bounce, tabela própria).
-- Rodar 1 vez: Supabase Dashboard -> SQL Editor -> New query -> colar -> Run.
-- Pode rodar de novo sem problema (recria função e view; a tabela só é criada se não existir).
--
-- Pontuação (calculada NO SERVIDOR a partir dos dados da run; o cliente não manda o score):
--   score = 20000 - 10 * time_sec - 25 * cards_thrown + 150 * criticals   (mínimo 1)
--   -> menos tempo e menos cartas = melhor; mais críticos = melhor. O tempo pesa mais.
-- Para mudar os pesos: altere dto_score() abaixo E LeaderboardClient.Score() no jogo, e rode de novo.

create table if not exists public.dto_runs (
  id            bigint generated always as identity primary key,
  name          text        not null check (char_length(name) between 2 and 14
                                            and name ~ '^[A-Za-z0-9À-ÿ _.\-]+$'),
  score         integer     not null,
  time_sec      integer     not null check (time_sec between 30 and 36000),
  cards_thrown  integer     not null check (cards_thrown between 0 and 100000),
  criticals     integer     not null check (criticals between 0 and 100000),
  fusions       integer     not null check (fusions between 0 and 100000),
  damage_taken  integer     not null check (damage_taken between 0 and 100000),
  deaths        integer     not null check (deaths between 0 and 100000),
  platform      text        not null default 'web',
  created_at    timestamptz not null default now()
);
create index if not exists dto_runs_score_idx on public.dto_runs (score desc);

-- RLS: a chave pública (anon/publishable) só LÊ. Inserções só pela função abaixo.
alter table public.dto_runs enable row level security;
drop policy if exists "public read" on public.dto_runs;
create policy "public read" on public.dto_runs for select to anon using (true);

create or replace function public.dto_score(p_time integer, p_cards integer, p_crits integer)
returns integer language sql immutable as $$
  select greatest(1, 20000 - 10 * p_time - 25 * p_cards + 150 * p_crits);
$$;

create or replace function public.dto_submit_run(
  p_name text, p_time integer, p_cards integer, p_crits integer,
  p_fusions integer, p_damage integer, p_deaths integer, p_platform text)
returns integer
language plpgsql
security definer
set search_path = public
as $$
declare
  clean_name text := btrim(regexp_replace(p_name, '\s+', ' ', 'g'));
  s integer;
begin
  -- Plausibilidade: o jogo tem 3 chefes (12 + 16 + 20 de vida); dano máximo por carta é 5
  -- (crítico de Espadas), então são precisas pelo menos ~10 cartas e ~60 s para vencer.
  if p_time < 60 or p_cards < 10 then raise exception 'implausible'; end if;
  if p_crits > p_cards then raise exception 'implausible'; end if;
  if p_cards > p_time * 6 then raise exception 'implausible'; end if;   -- cooldown de arremesso 0,22 s
  if p_fusions > p_time then raise exception 'implausible'; end if;
  -- Filtro de nome (o cliente também filtra).
  if lower(clean_name) ~ '(fuck|shit|bitch|nigg|fag|cunt|porn|sex|nazi|hitler|porra|caralho|buceta|puta|viado|merda|foda|pinto|cuzao|arrombad|mierda|puto|coño|pendej|verga|joder)' then
    raise exception 'name';
  end if;
  -- Limite leve: 1 envio por nome a cada 20 s.
  if exists (select 1 from dto_runs where lower(name) = lower(clean_name)
             and created_at > now() - interval '20 seconds') then
    raise exception 'too fast';
  end if;
  s := dto_score(p_time, p_cards, p_crits);
  insert into dto_runs (name, score, time_sec, cards_thrown, criticals, fusions, damage_taken, deaths, platform)
  values (clean_name, s, p_time, p_cards, p_crits, p_fusions, p_damage, p_deaths,
          case when p_platform ~ '^[a-z]{2,16}$' then p_platform else 'web' end);
  return s;
end;
$$;
revoke all on function public.dto_submit_run(text, integer, integer, integer, integer, integer, integer, text) from public;
grant execute on function public.dto_submit_run(text, integer, integer, integer, integer, integer, integer, text) to anon;

-- Melhor run de cada nome (é o que o jogo mostra).
drop view if exists public.dto_top;
create view public.dto_top with (security_invoker = true) as
select distinct on (lower(name))
       name, score, time_sec, cards_thrown, criticals, fusions, damage_taken, deaths, created_at
from public.dto_runs
order by lower(name), score desc, created_at asc;
grant select on public.dto_top to anon;
