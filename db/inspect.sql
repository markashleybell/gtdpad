use gtdpad

select 'ACTIVE' as Status, * from pages where deleted is null order by display_order
select 'ACTIVE' as Status, * from lists where deleted is null order by page_id, display_order
select 'ACTIVE' as Status, * from items where deleted is null order by list_id, display_order

select 'DELETED' as Status, * from pages where deleted is not null order by display_order
select 'DELETED' as Status, * from lists where deleted is not null order by page_id, display_order
select 'DELETED' as Status, * from items where deleted is not null order by list_id, display_order

--delete from pages
--delete from items
--delete from lists

