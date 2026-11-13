-- Draw the Odds: ranking SEPARADO por modo (easy / normal / hardcore).
-- Rodar 1 vez DEPOIS do draw_the_odds_setup.sql: Supabase Dashboard -> SQL Editor -> New query -> colar -> Run.
-- Pode rodar de novo sem problema. As runs antigas ficam como 'normal'.
-- Builds antigas continuam funcionando (a função antiga, sem p_mode, envia como 'normal').

alter table public.dto_runs add column if not exists mode text not null default 'normal';
alter table public.dto_runs drop constraint if exists dto_runs_mode_check;
alter table public.dto_runs add constraint dto_runs_mode_check check (mode in ('easy', 'normal', 'hardcore'));
create index if not exists dto_runs_mode_score_idx on public.dto_runs (mode, score desc);

-- Nova versão da função de envio, com o modo (mesmas checagens da original).
create or replace function public.dto_submit_run(
  p_name text, p_time integer, p_cards integer, p_crits integer,
  p_fusions integer, p_damage integer, p_deaths integer, p_platform text, p_mode text)
returns integer
language plpgsql
security definer
set search_path = public
as $$
declare
  clean_name text := btrim(regexp_replace(p_name, '\s+', ' ', 'g'));
  s integer;
begin
  if p_mode not in ('easy', 'normal', 'hardcore') then raise exception 'implausible'; end if;
  if p_time < 60 or p_cards < 10 then raise exception 'implausible'; end if;
  if p_crits > p_cards then raise exception 'implausible'; end if;
  if p_cards > p_time * 6 then raise exception 'implausible'; end if;
  if p_fusions > p_time then raise exception 'implausible'; end if;
  if lower(clean_name) ~ '(fuck|shit|bitch|nigg|fag|cunt|porn|sex|nazi|hitler|porra|caralho|buceta|puta|viado|merda|foda|pinto|cuzao|arrombad|mierda|puto|coño|pendej|verga|joder)' then
    raise exception 'name';
  end if;
  if exists (select 1 from dto_runs where lower(name) = lower(clean_name)
             and created_at > now() - interval '20 seconds') then
    raise exception 'too fast';
  end if;
  s := dto_score(p_time, p_cards, p_crits);
  insert into dto_runs (name, score, time_sec, cards_thrown, criticals, fusions, damage_taken, deaths, platform, mode)
  values (clean_name, s, p_time, p_cards, p_crits, p_fusions, p_damage, p_deaths,
          case when p_platform ~ '^[a-z]{2,16}$' then p_platform else 'web' end, p_mode);
  return s;
end;
$$;
revoke all on function public.dto_submit_run(text, integer, integer, integer, integer, integer, integer, text, text) from public;
grant execute on function public.dto_submit_run(text, integer, integer, integer, integer, integer, integer, text, text) to anon;

-- Melhor run de cada nome EM CADA MODO (o jogo filtra com ?mode=eq.easy etc.).
drop view if exists public.dto_top;
create view public.dto_top with (security_invoker = true) as
select distinct on (mode, lower(name))
       name, score, time_sec, cards_thrown, criticals, fusions, damage_taken, deaths, mode, created_at
from public.dto_runs
order by mode, lower(name), score desc, created_at asc;
grant select on public.dto_top to anon;
