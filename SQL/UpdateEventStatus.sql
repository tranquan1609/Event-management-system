CREATE PROCEDURE [dbo].[UpdateEventStatus]
AS
BEGIN
    -- Cập nhật status thành 'Completed' cho các sự kiện đã kết thúc
    UPDATE Events
    SET Status = 'Completed'
    WHERE EndTime < GETUTCDATE()
    AND Status = 'Upcoming';

    -- Cập nhật status thành 'In Progress' cho các sự kiện đang diễn ra
    UPDATE Events
    SET Status = 'In Progress'
    WHERE StartTime <= GETUTCDATE()
    AND EndTime > GETUTCDATE()
    AND Status = 'Upcoming';
END 