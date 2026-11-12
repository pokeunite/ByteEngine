using System.ComponentModel;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningOperation3D
{
 [Category("Company account"),Description("Protected deposits toward the level target. Camp upgrades spend Credits, never this account.")]
 public int CompanyAccountCredits{get;private set;}
 [Category("Company account")] public int CompanyDepositPercent{get;private set;}=50;
 int companyDepositRemainder;
 public void SetCompanyDepositPercent(int percent,bool save=true){CompanyDepositPercent=Math.Clamp(percent,0,100);if(save)SaveProgress();}
 // Integer hundredths carry forward so splitting a sale into tiny shipments
 // cannot change the total deposited. Refunds and starting credits are not sales.
 public (int Company,int Camp) PreviewSaleIncome(int income,int? lockedPercent=null){income=Math.Clamp(income,0,1_000_000_000);int deposit=(int)(((long)income*Math.Clamp(lockedPercent??CompanyDepositPercent,0,100)+companyDepositRemainder)/100);return(deposit,income-deposit);}
 internal (int Company,int Camp) CreditSaleIncome(long income,int? lockedPercent=null)
 {
  income=Math.Clamp(income,0,1_000_000_000);long scaled=income*Math.Clamp(lockedPercent??CompanyDepositPercent,0,100)+companyDepositRemainder;
  int deposit=(int)(scaled/100);companyDepositRemainder=(int)(scaled%100);int camp=(int)income-deposit;
  CompanyAccountCredits=Add(CompanyAccountCredits,deposit);Credits=Add(Credits,camp);return(deposit,camp);
 }
}
