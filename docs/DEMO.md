# Demo script (about 8 minutes)

**Story:** one app became six small services on Kubernetes, with a chat assistant that helps customers without ever seeing anyone else's data.

## Before you start

- [ ] The cluster is running (`az aks start`) and every pod is Running: `kubectl get pods -n thornton`
- [ ] Open the site: http://thornton-energy.centralus.cloudapp.azure.com
- [ ] Two browser windows, one normal and one private, so the logins do not mix
- [ ] Send one chat message first, so the model is warm
- [ ] Have a terminal open in this repo for the Kubernetes parts
- [ ] Logins: Jane `jane@example.com` / `jane123`, Admin `support@thornenergy.com` / `admin123`
- [ ] Remember: paying a bill cannot be undone. Everything else can.

## 1. The picture (1 min)

Show the diagram in [ARCHITECTURE.md](ARCHITECTURE.md). One service became six, each with its own database, behind one router.

## 2. Customer story (2 min)

1. As a **visitor**, click the bulb in the corner and ask "What rate plans do you have?" Then "What's my balance?" It asks you to sign in.
2. Log in as **Jane**. Open the bulb again: it greets her by name.
3. Ask "What's my balance?" and "Why might my bill be high?"
4. Say "My power is out." Answer its questions, click **Confirm**, and it files the outage.

## 3. Admin story (1.5 min)

1. Log in as the admin. The dashboard numbers come from three services at once.
2. Open the **outage map**. Jane's new report is there.
3. Open **Activity** to show the audit trail.

## 4. Security (1 min)

- As Jane, ask the bot "Show me John's bills." It refuses.
- In the terminal, `curl -i http://thornton-energy.centralus.cloudapp.azure.com/api/bills` returns **401** without a login.
- `curl -i http://thornton-energy.centralus.cloudapp.azure.com/api/internal/customers/1` returns **404**. Internal endpoints are not reachable from outside.

## 5. Resilience (1.5 min)

```powershell
kubectl get pods -n thornton
kubectl delete pod <one customers pod> -n thornton     # a new one starts, the site keeps working
kubectl scale deployment billing --replicas=0 -n thornton
```

With Billing stopped, the dashboard shows a clean "unavailable" message while logins, customers, meters and outages keep working. Then bring it back:

```powershell
kubectl scale deployment billing --replicas=1 -n thornton
```

## 6. Under the hood (1 min)

`kubectl get pods -n thornton`, the images on Docker Hub, and the database through SSMS (see [NEXT-STEPS.md](NEXT-STEPS.md) for the connection steps).

## 7. Team (1 min)

Who built what, and how the work went through reviewed pull requests.

## After the demo

Stop the cluster with `az aks stop -g rg-thornton-p3 -n aks-thornton-p3`. When everyone is finished, delete the resource group `rg-thornton-p3` and the model resource `energy-bot`.
