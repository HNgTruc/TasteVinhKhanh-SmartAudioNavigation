-- Sync PoiPoints đúng với 12 quán trong database gốc TasteVinhKhanh.sql
USE TasteVinhKhanhDb;
GO

UPDATE [dbo].[PoiPoints] SET
    [Name] = s.[Name],
    [ShortDescription] = s.[ShortDescription],
    [Latitude] = s.[Latitude],
    [Longitude] = s.[Longitude],
    [TriggerRadiusMeters] = s.[TriggerRadiusMeters],
    [Priority] = s.[Priority],
    [UpdatedAt] = GETUTCDATE()
FROM (
    VALUES
        (1,  N'Alo Quán',                   N'333 Vĩnh Khánh – Quán ăn đa dạng: hải sản tươi, nướng, lẩu. Không gian rộng rãi, thực đơn phong phú hơn 50 món ăn. Bắt buộc thử: bò cuốn kim chi, ba chỉ cuộn giòn và hải sản nướng.', 10.7607671, 106.7036279, 50, 10, 1),
        (2,  N'THÈM NƯỚNG YAKINIKU',      N'122 Vĩnh Khánh – Quán nướng Nhật cao cấp, thịt bò Wagyu tươi sống nướng ngay tại bàn. Không gian hiện đại, phù hợp nhóm bạn và gia đình. Trải nghiệm ẩm thực Nhật chính hiệu tại trung tâm phố Vĩnh Khánh.', 10.7607671, 106.7036279, 50, 9, 1),
        (3,  N'Chilli Lẩu Nướng Quán',      N'232 Vĩnh Khánh – Quán lẩu Thái, lẩu Hàn, hơn 50 món tươi mỗi ngày. Đa dạng nước dùng: lẩu Thái chua cay, lẩu kim chi Hàn Quốc. Không gian rộng rãi, giá cả phải chăng.', 10.7606591, 106.7037663, 50, 9, 1),
        (4,  N'A FAT HOT POT',               N'668 Vĩnh Khánh – Quán lẩu hot nhất phố, phong cách Hong Kong độc đáo. Thực đơn: lẩu Tứ Xuyên cay nồng, lẩu Tomyum chua thơm và lẩu sữa bơ. Nguyên liệu hải sản tươi sống cập nhật mỗi ngày.', 10.7606578, 106.7037689, 50, 8, 1),
        (5,  N'Lãng Quán',                  N'531 Vĩnh Khánh – Quán nướng lẩu hơn 40 món từ giòn rụm đến hải sản nướng. Không gian rộng, phục vụ khuya, cuối tuần luôn đông khách. Địa điểm quen thuộc của dân Sài Gòn mê ẩm thực.', 10.7610569, 106.7053027, 50, 8, 1),
        (6,  N'Lẩu Nướng Thuận Việt',       N'424 Vĩnh Khánh – Quán lẩu miền Trung với hương vị đậm đà. Thực đơn đa dạng, món từ 30,000đ. Điểm nhấn là nước dùng đậm đà và công thức nước chấm gia truyền. Lựa chọn tuyệt vời cho ngân sách tiết kiệm.', 10.7615, 106.7060, 50, 7, 1),
        (7,  N'Ốc Hoa Kiều',                N'598 Vĩnh Khánh – Quán ốc hơn 30 loại tươi: hấp, xào, nướng, lẩu. Hải sản nhập từ biển mỗi sáng. Bắt buộc thử: ốc bươu rang muối, ốc len xào dừa và càng cua rang me. Quán lâu đời trên phố Vĩnh Khánh.', 10.7620, 106.7065, 50, 7, 1),
        (8,  N'RONGbuffet',                  N'122 Vĩnh Khánh – Quán buffet hải sản cao cấp hơn 80 món tươi, chỉ từ 199,000đ. Khu vực nướng trong nhà tiện nghi. Hải sản tươi: tôm, cua, ghẹ, nghêu và ốc đặc biệt. Trải nghiệm buffet hải sản ngon nhất phố Vĩnh Khánh.', 10.7625, 106.7070, 50, 6, 1),
        (9,  N'SHAOKAO',                    N'424 Vĩnh Khánh – Quán nướng Trung-Việt độc đáo, kết hợp tinh hoa ẩm thực hai nền ẩm thực. Các món nướng được ướp theo công thức gia truyền. Điểm nhấn là không gian ngoài trời thoáng mát, phù hợp tiệc lớn.', 10.7630, 106.7075, 50, 6, 1),
        (10, N'Lẩu Gà Lá É',                N'18 Vĩnh Khánh – Quán chuyên lẩu gà nấu với nước dùng từ lá thảo mộc đặc trưng. Gà ta tự nhiên, thịt dai ngon. Ngoài ra có gà nướng, gà xào và các món gà miền Trung đặc sắc. Không gian rộng phù hợp gia đình và nhóm bạn.', 10.7635, 106.7080, 50, 5, 1),
        (11, N'BONA Food and Beer',          N'122 Vĩnh Khánh – Quán ăn địa phương đa dạng từ hải sản đến các món Việt cổ điển. Điểm nhấn: không gian thoáng mát, giá cả hợp lý, phục vụ khuya. Các món ốc và hải sản cập nhật mỗi ngày. Điểm dừng chân lý tưởng khuya trên phố Vĩnh Khánh.', 10.7640, 106.7085, 50, 4, 1),
        (12, N'SINZIEN Quán Nước',           N'375 Vĩnh Khánh – Quán nước giải khát nằm dọc phố ẩm thực, là điểm dừng chân lý tưởng sau bữa ăn. Thực đơn đa dạng từ sinh tố, nước ép trái cây đến các loại trà và cà phê. Không gian mát mẻ, phục vụ nhanh chóng.', 10.7617, 106.7022, 40, 3, 1)
) AS s([Id], [Name], [ShortDescription], [Latitude], [Longitude], [TriggerRadiusMeters], [Priority], [IsActive])
WHERE [PoiPoints].[Id] = s.[Id];

-- XÓA BẢNG SeedData (không cần nữa vì dữ liệu đã có trong TasteVinhKhanh.sql)
-- Bỏ comment dòng dưới nếu muốn xóa luôn:
-- DROP TABLE IF EXISTS SeedData;

PRINT N'✅ Đã sync 12 POIs đúng với database gốc!';
GO

-- Kiểm tra
SELECT Id, Name, Latitude, Longitude, Priority FROM [dbo].[PoiPoints] ORDER BY Priority DESC;
