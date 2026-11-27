-- Draw the Odds: ranking v2 (27/11/2026) — 5 chefes + Ás, coringas e visual do jogador no ranking.
-- Rodar 1 vez DEPOIS de draw_the_odds_setup.sql, draw_the_odds_modes.sql e draw_the_odds_endless.sql:
-- Supabase Dashboard -> SQL Editor -> New query -> colar -> Run. Pode rodar de novo sem problema.
--
-- O que muda:
--  * colunas novas em dto_runs: outfit ("chapéu.roupa.verso"), jokers ("0,5,13"), bosses (chefes vencidos), ver (versão do ranking)
--  * ver = 2 para as runs novas: a lista v2 começa ZERADA (as runs antigas eram de um jogo com 3 chefes e
--    outra tabela de dano). As antigas continuam na tabela e na view dto_top (builds antigas seguem funcionando).
--  * pontuação v2: 60000 - 8 * tempo - 15 * cartas + 200 * críticos (mínimo 1). Mesma fórmula em LeaderboardClient.Score.
--  * dto_player_best(nome, modo): a melhor run daquele nome e a posição dela (o jogo mostra "SEU RECORDE").

alter table public.dto_runs add column if not exists outfit text not null default '';
alter table public.dto_runs add column if not exists jokers text not null default '';
alter table public.dto_runs add column if not exists bosses integer not null default 0;
alter table public.dto_runs add column if not exists ver integer not null default 1;
create index if not exists dto_runs_ver_mode_score_idx on public.dto_runs (ver, mode, score desc);

create or replace function public.dto_score2(p_time integer, p_cards integer, p_crits integer)
returns integer language sql immutable as $$
  select greatest(1, 60000 - 8 * p_time - 15 * p_cards + 200 * p_crits);
$$;

-- Checagens comuns (nome, visual, coringas, limite de envio). Levanta exceção se algo não bate.
create or replace function public.dto_check2(p_name text, p_outfit text, p_jokers text)
returns text
language plpgsql
security definer
set search_path = public
as $$
declare
  clean_name text := btrim(regexp_replace(p_name, '\s+', ' ', 'g'));
begin
  if char_length(clean_name) < 2 or char_length(clean_name) > 14 or clean_name !~ '^[A-Za-z0-9À-ÿ _.\-]+$' then
    raise exception 'name';
  end if;
  if lower(clean_name) ~ '(fuck|shit|bitch|nigg|fag|cunt|porn|sex|nazi|hitler|porra|caralho|buceta|puta|viado|merda|foda|pinto|cuzao|arrombad|mierda|puto|coño|pendej|verga|joder)' then
    raise exception 'name';
  end if;
  if p_outfit !~ '^([0-9]{1,2}\.[0-9]{1,2}\.[0-9]{1,2})?$' then raise exception 'implausible'; end if;
  if p_jokers !~ '^([0-9]{1,2}(,[0-9]{1,2}){0,4})?$' then raise exception 'implausible'; end if;
  if exists (select 1 from dto_runs where lower(name) = lower(clean_name)
             and created_at > now() - interval '20 seconds') then
    raise exception 'too fast';
  end if;
  return clean_name;
end;
$$;
revoke all on function public.dto_check2(text, text, text) from public;

-- História (easy / normal / hardcore): run que venceu o Rei (5 chefes; com o Ás, 6).
create or replace function public.dto_submit_run2(
  p_name text, p_time integer, p_cards integer, p_crits integer,
  p_fusions integer, p_damage integer, p_deaths integer, p_platform text, p_mode text,
  p_outfit text, p_jokers text, p_bosses integer)
returns integer
language plpgsql
security definer
set search_path = public
as $$
declare
  clean_name text;
  s integer;
begin
  if p_mode not in ('easy', 'normal', 'hardcore') then raise exception 'implausible'; end if;
  -- 5 chefes (~110 de vida no total): pelo menos ~2 min e ~25 cartas
  if p_bosses < 5 or p_bosses > 6 then raise exception 'implausible'; end if;
  if p_time < 120 or p_time > 36000 or p_cards < 25 then raise exception 'implausible'; end if;
  if p_crits > p_cards then raise exception 'implausible'; end if;
  if p_cards > p_time * 6 then raise exception 'implausible'; end if;
  if p_fusions > p_time then raise exception 'implausible'; end if;
  clean_name := dto_check2(p_name, p_outfit, p_jokers);
  s := dto_score2(p_time, p_cards, p_crits);
  insert into dto_runs (name, score, time_sec, cards_thrown, criticals, fusions, damage_taken, deaths, platform, mode, outfit, jokers, bosses, ver)
  values (clean_name, s, p_time, p_cards, p_crits, p_fusions, p_damage, p_deaths,
          case when p_platform ~ '^[a-z]{2,16}$' then p_platform else 'web' end, p_mode, p_outfit, p_jokers, p_bosses, 2);
  return s;
end;
$$;
revoke all on function public.dto_submit_run2(text, integer, integer, integer, integer, integer, integer, text, text, text, text, integer) from public;
grant execute on function public.dto_submit_run2(text, integer, integer, integer, integer, integer, integer, text, text, text, text, integer) to anon;

-- Infinito: pontos = rodadas vencidas * 100000 + (99999 - segundos)
create or replace function public.dto_submit_endless2(
  p_name text, p_time integer, p_cards integer, p_crits integer,
  p_fusions integer, p_damage integer, p_deaths integer, p_platform text, p_rounds integer,
  p_outfit text, p_jokers text)
returns integer
language plpgsql
security definer
set search_path = public
as $$
declare
  clean_name text;
  s integer;
begin
  if p_rounds < 1 or p_rounds > 500 then raise exception 'implausible'; end if;
  if p_time < greatest(30, p_rounds * 25) or p_time > 36000 then raise exception 'implausible'; end if;
  if p_cards < p_rounds * 3 then raise exception 'implausible'; end if;
  if p_crits > p_cards then raise exception 'implausible'; end if;
  if p_cards > p_time * 6 then raise exception 'implausible'; end if;
  if p_fusions > p_time then raise exception 'implausible'; end if;
  clean_name := dto_check2(p_name, p_outfit, p_jokers);
  s := p_rounds * 100000 + greatest(0, 99999 - p_time);
  insert into dto_runs (name, score, time_sec, cards_thrown, criticals, fusions, damage_taken, deaths, platform, mode, outfit, jokers, bosses, ver)
  values (clean_name, s, p_time, p_cards, p_crits, p_fusions, p_damage, p_deaths,
          case when p_platform ~ '^[a-z]{2,16}$' then p_platform else 'web' end, 'endless', p_outfit, p_jokers, p_rounds, 2);
  return s;
end;
$$;
revoke all on function public.dto_submit_endless2(text, integer, integer, integer, integer, integer, integer, text, integer, text, text) from public;
grant execute on function public.dto_submit_endless2(text, integer, integer, integer, integer, integer, integer, text, integer, text, text) to anon;

-- Melhor run de cada nome em cada modo, só v2.
drop view if exists public.dto_top2;
create view public.dto_top2 with (security_invoker = true) as
select distinct on (mode, lower(name))
       name, score, time_sec, cards_thrown, criticals, fusions, damage_taken, deaths, mode, outfit, jokers, bosses, created_at
from public.dto_runs
where ver = 2
order by mode, lower(name), score desc, created_at asc;
grant select on public.dto_top2 to anon;

-- "SEU RECORDE": a melhor run de um nome num modo + a posição dela no ranking.
create or replace function public.dto_player_best(p_name text, p_mode text)
returns table (name text, score integer, time_sec integer, cards_thrown integer, criticals integer, fusions integer,
               damage_taken integer, deaths integer, outfit text, jokers text, bosses integer, rank bigint)
language sql
stable
security invoker
set search_path = public
as $$
  with best as (
    select * from dto_top2 t where t.mode = p_mode and lower(t.name) = lower(btrim(p_name)) limit 1
  )
  select b.name, b.score, b.time_sec, b.cards_thrown, b.criticals, b.fusions, b.damage_taken, b.deaths,
         b.outfit, b.jokers, b.bosses,
         (select count(*) + 1 from dto_top2 o where o.mode = p_mode and o.score > b.score)
  from best b;
$$;
grant execute on function public.dto_player_best(text, text) to anon;
