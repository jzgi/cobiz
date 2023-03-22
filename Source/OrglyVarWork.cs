﻿using ChainFx.Web;

namespace ChainSmart
{
    public abstract class OrglyVarWork : WebWork
    {
        public void @default(WebContext wc)
        {
            var org = wc[0].As<Org>();
            var prin = (User)wc.Principal;

            wc.GivePage(200, h =>
            {
                h.TOPBARXL_();

                bool astack = wc.Query[nameof(astack)];
                if (astack)
                {
                    h.T("<a class=\"uk-icon-button\" href=\"javascript: window.parent.closeUp(false);\" uk-icon=\"icon: chevron-left; ratio: 1.75\"></a>");
                }

                string rol = wc.Super ? "代" + User.Orgly[wc.Role] : User.Orgly[wc.Role];

                h.HEADER_("uk-width-expand uk-col uk-padding-left");
                h.H2(org.name);
                if (org.IsParent) h.H4(org.Ext);
                h.P2(prin.name, rol, brace: true);
                h._HEADER();

                if (org.icon)
                {
                    h.PIC(MainApp.WwwUrl, "/org/", org.id, "/icon", circle: true, css: "uk-width-small");
                }
                else
                    h.PIC(org.IsOfShop ? "/shp.webp" : org.IsCenter ? "/ctr.webp" : "/src.webp", circle: true, css: "uk-width-small");

                h._TOPBARXL();

                h.WORKBOARD(notice: org.id);

                // qrcode
                if (org.IsOfShop)
                {
                    h.NAV_(css: "uk-col uk-margin uk-flex-middle").QRCODE(MainApp.WwwUrl + "/" + org.MarketId + "/", css: " uk-width-small").SPAN("推荐市场")._NAV();
                }
            }, false, 30, title: org.name);
        }
    }


    [OrglyAuthorize(Org.TYP_SHP)]
    [Ui("市场操作")]
    public class ShplyVarWork : OrglyVarWork
    {
        protected override void OnCreate()
        {
            // org

            CreateWork<OrglySetgWork>("setg");

            CreateWork<OrglyAccessWork>("access", true); // true = shop

            CreateWork<OrglyCreditWork>("credit");

            CreateWork<PtylyBuyClearWork>("buyclr", state: true);

            // shp

            CreateWork<ShplyItemWork>("sitem");

            CreateWork<ShplyVipWork>("svip");

            CreateWork<ShplyBuyWork>("sbuy");

            CreateWork<ShplyPosWork>("spos");

            CreateWork<ShplyBuyAggWork>("sbuyagg");

            CreateWork<ShplyBookWork>("sbook");


            // mkt

            CreateWork<MktlyOrgWork>("morg");

            CreateWork<MktlyTestWork>("mtest");

            CreateWork<MktlyBuyWork>("mbuy");

            CreateWork<MktlyBookWork>("mbook");
        }
    }


    [OrglyAuthorize(Org.TYP_SRC)]
    [Ui("供应操作")]
    public class SrclyVarWork : OrglyVarWork
    {
        protected override void OnCreate()
        {
            // org

            CreateWork<OrglySetgWork>("setg");

            CreateWork<OrglyAccessWork>("access", false); // false = source

            CreateWork<OrglyCreditWork>("credit");

            CreateWork<PtylyBookClearWork>("bookclr", state: true); // true = is org

            // src

            CreateWork<SrclyAssetWork>("sasset");

            CreateWork<SrclyLotWork>("slot");

            CreateWork<SrclyBookWork>("sbooks", state: Book.TYP_SPOT, ui: new("销售订单-现货", "商户"));

            CreateWork<SrclyBookWork>("sbookf", state: Book.TYP_LIFT, ui: new("销售订单-助农", "商户"));

            CreateWork<SrclyBookAggWork>("sbookldg");

            // ctr

            CreateWork<CtrlyOrgWork>("corg");

            CreateWork<CtrlyBookAggWork>("cbookagg");

            CreateWork<CtrlyBookWork>("cbook");

            CreateWork<CtrlyLotWork>("clot");
        }
    }
}