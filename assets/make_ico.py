"""Конвертирует логотип PNG в многослойный ICO с прозрачными углами."""
from PIL import Image, ImageChops, ImageDraw

SRC = "assets/wireshift-logo.png"
DST = "wireshift.ico"

img = Image.open(SRC).convert("RGBA")
w, h = img.size

# Обрезаем чёрную рамку: ищем ограничивающий прямоугольник нечёрных пикселей
diff = ImageChops.difference(img.convert("RGB"), Image.new("RGB", img.size, (0, 0, 0)))
bbox = diff.convert("L").point(lambda p: 255 if p > 24 else 0).getbbox()
if bbox:
    img = img.crop(bbox)
    w, h = img.size

# Квадрат + скруглённые углы с прозрачностью (маска с суперсэмплингом)
side = min(w, h)
img = img.crop(((w - side) // 2, (h - side) // 2,
                (w + side) // 2, (h + side) // 2))
SS = 4
mask = Image.new("L", (side * SS, side * SS), 0)
d = ImageDraw.Draw(mask)
d.rounded_rectangle([0, 0, side * SS - 1, side * SS - 1],
                    radius=int(side * SS * 0.23), fill=255)
mask = mask.resize((side, side), Image.LANCZOS)
img.putalpha(mask)

# Многослойный ICO: 16..256
sizes = [16, 24, 32, 48, 64, 128, 256]
img.save(DST, format="ICO", sizes=[(s, s) for s in sizes])
print("saved", DST)

# Превью 256 для проверки
img.resize((256, 256), Image.LANCZOS).save("assets/wireshift-ico-preview.png")
print("preview saved")
