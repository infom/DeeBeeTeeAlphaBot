CREATE TABLE gnode (ID INTEGER PRIMARY KEY, [uid] int, username varchar(500), balance numeric(10,2)) AS NODE;
CREATE TABLE gedge (amount numeric(10,2)) AS EDGE;


insert into gnode (ID, uid, username, balance)
select uid, uid, [user], limit from users

select * from gnode 

insert into gedge 
select (select $node_id from gnode where username= from_user), (select $node_id from gnode where username= to_user), amount from 
(
select from_user, to_user, amount from 
(
select from_user, to_user, sum(amount) amount
from(
select from_user, to_user, sum(amount) amount from transactions group by from_user, to_user
union 
select to_user, from_user, -sum(amount) amount from transactions group by to_user, from_user) as al
group by from_user, to_user
) al2
where amount <>0
) as e

delete from gedge 

select gnode2.username, gedge.amount
from gnode gnode1, gedge, gnode gnode2
where match(gnode1-(gedge)->gnode2) AND gnode1.username = 'infom'

select * from gedge where $from_id = '{"type":"node","schema":"dbo","table":"gnode","id":3}'
union select * from gedge where $to_id = '{"type":"node","schema":"dbo","table":"gnode","id":3}'


with cte as (
      select e.$from_id, e.$to_id, iscycle = 0
      from gedge e
      union all
      select cte.$from_id, e.$to_id,
             (case when cte.$from_id = e.$to_id then 1 else 0 end) as iscycle
      from cte join
           gedge e
           on cte.to_id = e.$from_id
      where iscycle = 0
     )
select max(iscycle)
from cte;
