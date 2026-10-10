// 編輯器的匯出格式（Godot 版讀取方式見 godot/src/Content/CustomContent.cs）

const round = v => Math.round(v * 10) / 10;

// 圖片的範圍：中心 (x, y)、基本寬高 (w, h) 乘上縮放
export function boxOf({ x, y, w, h, scale }) {
  return [round(x - (w * scale) / 2), round(y - (h * scale) / 2), round(x + (w * scale) / 2), round(y + (h * scale) / 2)];
}

// 文件\DeskPet\accessories\<名稱>\accessory.json
export function accessoryJson(name, slot, img) {
  return { name, slot, image: img.fileName, box: boxOf(img) };
}

// 文件\DeskPet\outfits\<名稱>\outfit.json
export function outfitJson(name, character, parts) {
  const out = { name, character, parts: {} };
  for (const [art, img] of Object.entries(parts)) out.parts[art] = { image: img.fileName, box: boxOf(img) };
  return out;
}

// 文件\DeskPet\characters\<名稱>\character.json：base 是當作身形與動作範本的角色
export function characterJson(c, base) {
  const { parts } = outfitJson(c.name, base, c.parts);
  const faces = c.faces.place
    ? { box: boxOf(c.faces.place), images: Object.fromEntries(Object.entries(c.faces.images).map(([k, v]) => [k, v.fileName])) }
    : null;
  return { name: c.name, description: c.desc, base, parts, faces, lines: c.greet ? { greet: [c.greet] } : {} };
}

export function download(fileName, data) {
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
  const a = document.createElement("a");
  a.href = URL.createObjectURL(blob);
  a.download = fileName;
  a.click();
  setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}

// 資料夾名稱：去掉 Windows 不允許的字元
export const folderName = name => (name.trim() || "我的作品").replace(/[\\/:*?"<>|]/g, "_");
