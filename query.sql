CREATE TABLE gnode (ID INTEGER PRIMARY KEY, [uid] int, username varchar(500), balance numeric(10,2)) AS NODE;
CREATE TABLE gedge (amount numeric(10,2)) AS EDGE;

--RegenData
delete from gnode
delete from gedge 

insert into gnode (ID, uid, username, balance)
select uid, uid, [user], limit from users

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
where amount >0
) as e
order by 3 desc

--GetBalance
select gnode2.username, gedge.amount
from gnode gnode1, gedge, gnode gnode2
where match(gnode1-(gedge)->gnode2) AND gnode1.username = 'infom'

