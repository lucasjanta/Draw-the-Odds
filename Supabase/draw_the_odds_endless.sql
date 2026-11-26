-- Draw the Odds: ranking do MODO INFINITO (mode = 'endless').
-- Rodar 1 vez DEPOIS de draw_the_odds_setup.sql e draw_the_odds_modes.sql:
-- Supabase Dashboard -> SQL Editor -> New query -> colar -> Run. Pode rodar de novo sem problema.
--
-- Pontos = chefes vencidos * 100000 + (99999 - segundos). Mais chefes sempre ganha; empate = mais rápido.
-- O jogo lê a mesma view dto_top filtrando ?mode=eq.endless e mostra pontos / 100000 como "BOSSES".

alter table public.dto_runs drop constraint if exists dto_runs_mode_check;
alter table public.dto_runs add constraint dto_runs_mode_check
  check (mode in ('easy', 'normal', 'hardcore', 'endless'));

create or replace function public.dto_submit_endless(
  p_name text, p_time integer, p_cards integer, p_crits integer,
  p_fusions integer, p_damage integer, p_deaths integer, p_platform text, p_rounds integer)
returns integer
language plpgsql
security definer
set search_path = public
as $$
declare
  clean_name text := btrim(regexp_replace(p_name, '\s+', ' ', 'g'));
  s integer;
begin
  -- Checagens de plausibilidade (cada chefe leva pelo menos ~25 s e algumas cartas)
  if p_rounds < 1 or p_rounds > 500 then raise exception 'implausible'; end if;
  if p_time < greatest(30, p_rounds * 25) or p_time > 36000 then raise exception 'implausible'; end if;
  if p_cards < p_rounds * 3 then raise exception 'implausible'; end if;
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
  s := p_rounds * 100000 + greatest(0, 99999 - p_time);
  insert into dto_runs (name, score, time_sec, cards_thrown, criticals, fusions, damage_taken, deaths, platform, mode)
  values (clean_name, s, p_time, p_cards, p_crits, p_fusions, p_damage, p_deaths,
          case when p_platform ~ '^[a-z]{2,16}$' then p_platform else 'web' end, 'endless');
  return s;
end;
$$;
revoke all on function public.dto_submit_endless(text, integer, integer, integer, integer, integer, integer, text, integer) from public;
grant execute on function public.dto_submit_endless(text, integer, integer, integer, integer, integer, integer, text, integer) to anon;
