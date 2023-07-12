using System;
using System.Data;
using System.Threading.Tasks;
using ChainFx.Web;
using static ChainFx.Web.Modal;
using static ChainSmart.WeixinUtility;
using static ChainFx.Application;
using static ChainFx.Nodal.Nodality;

namespace ChainSmart;

public abstract class BuyVarWork : WebWork
{
    public async Task @default(WebContext wc)
    {
        int id = wc[0];

        using var dc = NewDbContext();
        dc.Sql("SELECT ").collst(Buy.Empty).T(" FROM buys WHERE id = @1");
        var o = await dc.QueryTopAsync<Buy>(p => p.Set(id));

        wc.GivePane(200, h =>
        {
            h.UL_("uk-list uk-list-divider");
            if (o.IsPlat)
            {
                h.LI_().LABEL("买家").SPAN_("uk-static").T(o.uname).SP().A_TEL(o.utel, o.utel)._SPAN()._LI();
                h.LI_().LABEL(string.Empty).SPAN_("uk-static").T(o.ucom).T('-').T(o.uaddr)._SPAN()._LI();
                h.LI_().FIELD("服务费", o.fee, true)._LI();
            }
            h.LI_().FIELD("应付金额", o.topay, true).FIELD("实付金额", o.pay, true)._LI();

            if (o.creator != null) h.LI_().FIELD2("下单", o.created, o.creator)._LI();
            if (o.adapter != null) h.LI_().FIELD2(o.IsVoid ? "撤单" : "集合", o.adapted, o.adapter)._LI();
            if (o.oker != null) h.LI_().FIELD2(o.IsVoid ? "撤销" : "派发", o.oked, o.oker)._LI();

            h._UL();

            h.TABLE(o.items, d =>
            {
                h.TD_().T(d.name);
                if (d.unitw != 1)
                {
                    h.SP().SMALL_().T(d.unitw).T(d.unit).T("件")._SMALL();
                }

                h._TD();
                h.TD_(css: "uk-text-right").CNY(d.RealPrice).SP().SUB(d.unit)._TD();
                h.TD2(d.qty, d.unit, css: "uk-text-right");
                h.TD(d.SubTotal, true, true);
            });

            h.TOOLBAR(bottom: true, status: o.Status, state: o.ToState());
        });
    }
}

public class MyBuyVarWork : BuyVarWork
{
    public async Task ok(WebContext wc)
    {
        int id = wc[0];
        var prin = (User)wc.Principal;

        using var dc = NewDbContext();
        dc.Sql("UPDATE buys SET oked = @1, oker = @2, status = 4 WHERE id = @3 AND uid = @4 AND status = 2 RETURNING rtlid, pay");
        if (await dc.QueryTopAsync(p => p.Set(DateTime.Now).Set(prin.name).Set(id).Set(prin.id)))
        {
            dc.Let(out int rtlid);
            dc.Let(out decimal pay);

            var rtl = GrabTwin<int, Org>(rtlid);
            rtl.Notices.Put(OrgNoticePack.BUY_OKED, 1, pay);
        }

        wc.Give(200);
    }
}

public class RtllyBuyVarWork : BuyVarWork
{
    [Ui("回退", tip: "回退到收单状态", icon: "triangle-left", status: 2 | 4), Tool(ButtonConfirm, state: Buy.STA_REVERSABLE)]
    public async Task back(WebContext wc)
    {
        int id = wc[0];
        var org = wc[-2].As<Org>();

        using var dc = NewDbContext();
        dc.Sql("UPDATE buys SET adapted = NULL, adapter = NULL, oked = NULL, oker = NULL, status = 1 WHERE id = @1 AND rtlid = @2 AND (status = 2 OR status = 4)");
        await dc.ExecuteAsync(p => p.Set(id).Set(org.id));

        wc.Give(200);
    }

    [OrglyAuthorize(0, User.ROL_LOG)]
    [Ui("自派", "确认自行派送？", icon: "arrow-right", status: 1), Tool(ButtonConfirm)]
    public async Task ok(WebContext wc)
    {
        int id = wc[0];
        var org = wc[-2].As<Org>();
        var prin = (User)wc.Principal;

        using var dc = NewDbContext();
        dc.Sql("UPDATE buys SET oked = @1, oker = @2, status = 4 WHERE id = @3 AND rtlid = @4 AND status = 1 RETURNING uim, pay");
        if (await dc.QueryTopAsync(p => p.Set(DateTime.Now).Set(prin.name).Set(id).Set(org.id)))
        {
            dc.Let(out string uim);
            dc.Let(out decimal pay);

            await PostSendAsync(uim, $"商家自行派送，请留意收货（{org.name}，单号{id:D8}，￥{pay}）");
        }

        wc.Give(200);
    }

    [OrglyAuthorize(0, User.ROL_MGT)]
    [Ui("撤单", "确认撤单并且退款？", icon: "trash", status: 1 | 2), Tool(ButtonConfirm)]
    public async Task @void(WebContext wc)
    {
        int id = wc[0];
        var org = wc[-2].As<Org>();
        var prin = (User)wc.Principal;

        using var dc = NewDbContext(IsolationLevel.ReadCommitted);
        try
        {
            dc.Sql("UPDATE buys SET refund = pay, status = 0, adapted = @1, adapter = @2 WHERE id = @3 AND rtlid = @4 AND status = 1 RETURNING uim, topay, refund");
            if (await dc.QueryTopAsync(p => p.Set(DateTime.Now).Set(prin.name).Set(id).Set(org.id)))
            {
                dc.Let(out string uim);
                dc.Let(out decimal topay);
                dc.Let(out decimal refund);

                // remote call
                var trade_no = Buy.GetOutTradeNo(id, topay);
                string err = await PostRefundAsync(sup: false, trade_no, refund, refund, trade_no);
                if (err != null) // not success
                {
                    dc.Rollback();
                    Err(err);
                }
                else
                {
                    // notify user
                    await PostSendAsync(uim, "您的订单已经撤销，请查收退款（" + org.name + "　#" + trade_no + "　￥" + refund + "）");
                }
            }
        }
        catch (Exception)
        {
            dc.Rollback();
            Err("退款失败，订单号：" + id);
            return;
        }

        wc.Give(200);
    }
}

public class MktlyBuyVarWork : BuyVarWork
{
    public async Task com(WebContext wc)
    {
        string ucom = wc[0];
        var prin = (User)wc.Principal;
        var mkt = wc[-2].As<Org>();

        if (wc.IsGet)
        {
            using var dc = NewDbContext();

            dc.Sql("SELECT ").collst(Buy.Empty).T(" FROM buys WHERE mktid = @1 AND (status = 1 OR status = 2) AND typ = 1 AND ucom = @2");
            var arr = await dc.QueryAsync<Buy>(p => p.Set(mkt.id).Set(ucom));

            wc.GivePane(200, h =>
            {
                h.MAINGRID(arr, o =>
                {
                    h.UL_("uk-card-body uk-list uk-list-divider");
                    h.LI_().H4(o.utel).SPAN_("uk-badge").T(o.created, time: 0).SP().T(Buy.Statuses[o.status])._SPAN()._LI();

                    foreach (var it in o.items)
                    {
                        h.LI_();

                        h.SPAN_("uk-width-expand").T(it.name);
                        if (it.unitw > 0)
                        {
                            h.SP().SMALL_().T(it.unitw).T(it.unit)._SMALL();
                        }

                        h._SPAN();

                        h.SPAN_("uk-width-1-5 uk-flex-right").CNY(it.RealPrice).SP().SUB(it.unit)._SPAN();
                        h.SPAN_("uk-width-tiny uk-flex-right").T(it.qty).SP().T(it.unit)._SPAN();
                        h.SPAN_("uk-width-1-5 uk-flex-right").CNY(it.SubTotal)._SPAN();
                        h._LI();
                    }
                    h._LI();

                    h._UL();
                });

                h.FORM_().HIDDEN(nameof(com), 1).BOTTOM_BUTTON(prin.name + " 确认", nameof(com), post: true)._FORM();
            });
        }
        else // POST
        {
            // Note: update only status = 2
            using var dc = NewDbContext();
            dc.Sql("UPDATE buys SET oked = @1, oker = @2, status = 4 WHERE mktid = @3 AND status = 2 AND typ = 1 AND ucom = @4");
            await dc.ExecuteAsync(p => p.Set(DateTime.Now).Set(prin.name).Set(mkt.id).Set(ucom));

            wc.GivePane(200);
        }
    }
}