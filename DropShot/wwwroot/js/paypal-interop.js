window.PayPalInterop = {
    renderButton: function (containerId, planId, dotNetRef) {
        const container = document.getElementById(containerId);
        if (!container) return;
        container.innerHTML = '';

        paypal.Buttons({
            style: {
                shape: 'rect',
                color: 'gold',
                layout: 'vertical',
                label: 'subscribe'
            },
            createSubscription: function (data, actions) {
                return actions.subscription.create({ plan_id: planId });
            },
            onApprove: function (data) {
                dotNetRef.invokeMethodAsync('OnPayPalApproved', data.subscriptionID, data.payerID || '');
            },
            onCancel: function () {
                dotNetRef.invokeMethodAsync('OnPayPalCancelled');
            },
            onError: function (err) {
                console.error('PayPal error:', err);
                dotNetRef.invokeMethodAsync('OnPayPalError', err.toString());
            }
        }).render('#' + containerId);
    }
};
