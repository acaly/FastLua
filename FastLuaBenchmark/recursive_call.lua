local count = 0
local function f(level)
	if level == 5 then return end
	f(level + 1)
	f(level + 1)
	count = count + 1
end
f(0)
return count