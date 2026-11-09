// Use the author's public, free PNG SHEET export; no account or paid API.
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{const out=path.resolve('artifacts/particlefx-download');fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'chrome',headless:true});try{
 const page=await browser.newPage({acceptDownloads:true});
 await page.goto('https://www.particlefx.studio/effects/cartoon-explosion',{waitUntil:'domcontentloaded',timeout:90000});
 const button=page.getByRole('button',{name:'PNG SHEET',exact:true});await button.waitFor({timeout:90000});await page.waitForFunction(()=>[...document.querySelectorAll('button')].some(b=>b.textContent.trim()==='PNG SHEET'&&!b.disabled),null,{timeout:90000});
 const downloadPromise=page.waitForEvent('download',{timeout:180000});await button.click();const download=await downloadPromise;
 const file=path.join(out,download.suggestedFilename());await download.saveAs(file);console.log(JSON.stringify({file,name:download.suggestedFilename()}));
 await page.screenshot({path:path.join(out,'author-preview.png')});
 }finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
